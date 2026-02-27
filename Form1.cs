using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HikCameraCapture
{
    public partial class Form1 : Form
    {
        private CameraManager _cameraManager;
        private bool _isGrabbing;
        private Thread _captureThread;
        private readonly object _bufferLock = new object();
       // private CImage _driverImage;
        private CFrameSpecInfo _frameSpecInfo;
        private Bitmap _displayBitmap;
        private PixelFormat _bitmapPixelFormat;
        private volatile bool _uiUpdatePending = false;
        private readonly object _latestFrameLock = new object();
        private Bitmap _latestFrame = null;
        MyCamera.MV_SAVE_IMG_TO_FILE_PARAM _driverImage = new MyCamera.MV_SAVE_IMG_TO_FILE_PARAM();

        // Minimal holder for the last captured raw frame so the UI save button can
        // build a Bitmap and persist it on demand. Protected by _bufferLock.
        private class LastFrameInfo
        {
            public byte[] Data;
            public int Width;
            public int Height;
            public MvGvspPixelType PixelType;
            public int FrameLen;
        }

        private LastFrameInfo _lastFrameInfo = null;
        /// <summary>
        /// MyCamera SDK 的封装实例。所有对相机的操作均通过此实例调用 SDK 接口。
        /// </summary>
        private MyCamera m_MyCamera = new MyCamera();

        /// <summary>
        /// 保存当前帧信息（使用扩展结构 MV_FRAME_OUT_INFO_EX），供保存图片等操作使用。
        /// </summary>
        MyCamera.MV_FRAME_OUT_INFO_EX m_stFrameInfo = new MyCamera.MV_FRAME_OUT_INFO_EX();
        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
       
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _cameraManager = new CameraManager();
            EnumDevices();
        }

        private void EnumDevices()
        {
            try
            {
                _cameraManager.Initialize();
                cbDeviceList.Items.Clear();

                for (int i = 0; i < _cameraManager.DeviceList.Count; i++)
                {
                    var dev = _cameraManager.DeviceList[i];
                    if (dev.nTLayerType == CSystem.MV_GIGE_DEVICE)
                    {
                        var gigeInfo = (CGigECameraInfo)dev;
                        cbDeviceList.Items.Add($"GEV: {gigeInfo.UserDefinedName ?? gigeInfo.chModelName} ({gigeInfo.chSerialNumber})");
                    }
                    else if (dev.nTLayerType == CSystem.MV_USB_DEVICE)
                    {
                        var usbInfo = (CUSBCameraInfo)dev;
                        cbDeviceList.Items.Add($"U3V: {usbInfo.UserDefinedName ?? usbInfo.chModelName} ({usbInfo.chSerialNumber})");
                    }
                }

                if (_cameraManager.DeviceList.Count > 0)
                {
                    cbDeviceList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"EnumDevices failed: {ex.Message}");
                ShowError($"枚举设备失败: {ex.Message}", 0);
            }
        }

        private void btnOpenCamera_Click_1(object sender, EventArgs e)
        {
            try
            {
                OpenCameraCommand();
                CameraLogger.Log("OpenCamera command executed successfully");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"OpenCamera failed: {ex.Message}");
                ShowError($"打开相机失败: {ex.Message}", 0);
            }
        }

        public void OpenCameraCommand()
        {
            bnContinuesMode.Enabled = true;
            bnContinuesMode.Checked = true;
            bnTriggerMode.Enabled = true;

            if (_cameraManager.DeviceList.Count == 0 || cbDeviceList.SelectedIndex < 0)
            {
                CameraLogger.Log("OpenCameraCommand: no device selected");
                ShowError("请选择设备", 0);
                return;
            }

            _cameraManager.OpenCamera(cbDeviceList.SelectedIndex);
            ReadCameraParams();
            UpdateCtrlState(true);
        }

        private void btnStartCapture_Click_1(object sender, EventArgs e)
        {
            try
            {
                StartCaptureCommand();
                CameraLogger.Log("StartCapture command executed successfully");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"StartCapture failed: {ex.Message}");
                ShowError($"开始采集失败: {ex.Message}", 0);
            }
        }

        public void StartCaptureCommand()
        {
            _cameraManager.SetAcquisitionMode(MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            _cameraManager.SetTriggerMode(MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

            int ret = InitDisplayBitmap();
            if (ret != CErrorDefine.MV_OK)
            {
                CameraLogger.Log($"InitDisplayBitmap failed: 0x{ret:X}");
                return;
            }

            _isGrabbing = true;
            _captureThread = new Thread(CaptureThreadProc);
            _captureThread.IsBackground = true;
            _captureThread.Start();

            _cameraManager.StartGrabbing();

            btnStartCapture.Enabled = false;
            btnStopCapture.Enabled = true;
            btnSaveImage.Enabled = true;
        }

        public void StopCaptureCommand()
        {
            _isGrabbing = false;

            if (_captureThread != null && _captureThread.IsAlive)
            {
                // 等待线程完全退出，避免在后台线程仍在访问位图时释放资源
                _captureThread.Join();
            }

            _cameraManager.StopGrabbing();

            btnStartCapture.Enabled = true;
            btnStopCapture.Enabled = false;
            btnSaveImage.Enabled = true;
        }

        public void CloseCameraCommand()
        {
            if (_isGrabbing)
            {
                StopCaptureCommand();
            }

            _cameraManager.CloseCamera();

            // 确保后台线程已退出后再释放显示位图
            try
            {
                _displayBitmap?.Dispose();
                _displayBitmap = null;
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"Dispose _displayBitmap failed: {ex.Message}");
            }

            UpdateCtrlState(false);
        }

        private void CaptureThreadProc()
        {
            CFrameout frameOut = new CFrameout();
            CPixelConvertParam convertParam = new CPixelConvertParam();

            while (_isGrabbing)
            {
                int ret = _cameraManager.GetImageBuffer(ref frameOut, 1000);
                if (ret != CErrorDefine.MV_OK)
                {
                    bool isTriggerModeOn = false;
                    CEnumValue triggerMode = new CEnumValue();
                    int enumRet = _cameraManager.Camera.GetEnumValue("TriggerMode", ref triggerMode);

                    if (enumRet == CErrorDefine.MV_OK)
                    {
                        isTriggerModeOn = (triggerMode.CurValue == (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                    }

                    if (_isGrabbing && isTriggerModeOn)
                    {
                        Thread.Sleep(5);
                    }
                    continue;
                }

                byte[] outBuffer = null;
                int bmpWidth = 0, bmpHeight = 0;
                PixelFormat pf = _bitmapPixelFormat;
                int srcWidth = 0, srcHeight = 0, srcStride = 0;
                // 仅在需要保护的共享字段上加锁（driver image / frame spec）
                lock (_bufferLock)
                {
                    convertParam.InImage = frameOut.Image;
                   
                    if (frameOut.Image != null)
                    {
                        // Save via SDK helper that accepts a pointer. Build param and call.
                        try
                        {
                            _driverImage = new MyCamera.MV_SAVE_IMG_TO_FILE_PARAM();
                            _driverImage.enImageType = MyCamera.MV_SAVE_IAMGE_TYPE.MV_Image_Bmp;
                            _driverImage.enPixelType = m_stFrameInfo.enPixelType;
                            _driverImage.pData = frameOut.Image.ImageAddr;
                            _driverImage.nDataLen = (uint)m_stFrameInfo.nFrameLen;
                            _driverImage.nHeight = m_stFrameInfo.nHeight;
                            _driverImage.nWidth = m_stFrameInfo.nWidth;
                            _driverImage.iMethodValue = 2; // SDK defined
                            string fileName = "Image_w" + _driverImage.nWidth.ToString() + "_h" + _driverImage.nHeight.ToString() + "_fn" + m_stFrameInfo.nFrameNum.ToString() + ".bmp";
                            _driverImage.pImagePath = fileName;

                            int nRet = m_MyCamera.MV_CC_SaveImageToFile_NET(ref _driverImage);
                            if (nRet != MyCamera.MV_OK)
                            {
                                CameraLogger.Log($"MV_CC_SaveImageToFile_NET failed: 0x{nRet:X}");
                            }
                        }
                        catch (Exception ex)
                        {
                            CameraLogger.Log($"Save via SDK failed: {ex.Message}");
                        }

                        // Also keep a managed copy for UI Save dialog usage. Use ImageAddr if available.
                        try
                        {
                            if (frameOut.Image.ImageAddr != IntPtr.Zero && frameOut.Image.FrameLen > 0)
                            {
                                int len = checked((int)frameOut.Image.FrameLen);
                                byte[] data = new byte[len];
                                Marshal.Copy(frameOut.Image.ImageAddr, data, 0, len);
                                _lastFrameInfo = new LastFrameInfo
                                {
                                    Data = data,
                                    Width = frameOut.Image.Width,
                                    Height = frameOut.Image.Height,
                                    PixelType = frameOut.Image.PixelType,
                                    FrameLen = (int)frameOut.Image.FrameLen
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            CameraLogger.Log($"Copy to managed buffer failed: {ex.Message}");
                            _lastFrameInfo = null;
                        }
                    }
                    else
                    {
                        _driverImage = default;
                    }

                    if (frameOut.FrameSpec != null)
                    {
                        _frameSpecInfo = frameOut.FrameSpec;
                    }

                    convertParam.InImage = frameOut.Image;
                    if (_bitmapPixelFormat == PixelFormat.Format8bppIndexed)
                    {
                        convertParam.OutImage.PixelType = MvGvspPixelType.PixelType_Gvsp_Mono8;
                    }
                    else
                    {
                        convertParam.OutImage.PixelType = MvGvspPixelType.PixelType_Gvsp_BGR8_Packed;
                    }

                    _cameraManager.ConvertPixelType(ref convertParam);

                    // Determine source dimensions/stride from OutImage when available
                    try
                    {
                        if (convertParam.OutImage != null)
                        {
                            srcWidth = (int)convertParam.OutImage.Width;
                            srcHeight = (int)convertParam.OutImage.Height;
                            // If FrameLen is provided use it to compute per-line stride
                            try
                            {
                                int frameLen = (int)convertParam.OutImage.FrameLen;
                                if (frameLen > 0 && srcHeight > 0)
                                {
                                    srcStride = frameLen / srcHeight;
                                }
                            }
                            catch { /* ignore and fallback */ }
                        }
                    }
                    catch
                    {
                        // ignore - fallback to defaults below
                        srcWidth = 0; srcHeight = 0;
                    }

                    // 立刻拷贝输出像素数据到独立缓冲，FreeImageBuffer 后 SDK 可能会复用内存
                    if (convertParam.OutImage.ImageData != null && convertParam.OutImage.ImageData.Length > 0)
                    {
                        int len = convertParam.OutImage.ImageData.Length;
                        outBuffer = new byte[len];
                        Buffer.BlockCopy(convertParam.OutImage.ImageData, 0, outBuffer, 0, len);
                    }

                    // 使用 display bitmap 的尺寸作为目标尺寸，避免直接在后台线程访问/修改该对象
                    if (_displayBitmap != null)
                    {
                        bmpWidth = _displayBitmap.Width;
                        bmpHeight = _displayBitmap.Height;
                    }
                    else if (frameOut.Image != null)
                    {
                        bmpWidth = frameOut.Image.Width;
                        bmpHeight = frameOut.Image.Height;
                    }

                    // If OutImage didn't provide dimensions, fall back to bmp size
                    if (srcWidth == 0) srcWidth = bmpWidth;
                    if (srcHeight == 0) srcHeight = bmpHeight;

                    // Compute source stride as tightly packed row size (width * bytesPerPixel) only if we couldn't derive it
                    int bytesPerPixelLocalFinal = (_bitmapPixelFormat == PixelFormat.Format8bppIndexed) ? 1 : 3;
                    if (srcStride == 0)
                    {
                        srcStride = Math.Max(1, srcWidth * bytesPerPixelLocalFinal);
                    }
                }

                // 创建每帧独立的 Bitmap，使用源图像尺寸（若可用）或 display bitmap 尺寸作为目标尺寸，在 UI 线程安全地替换 PictureBox.Image
                // 保证在无法从 convertParam.OutImage 获得尺寸信息时仍能回退到 display bitmap 的尺寸，避免丢帧
                int dstWidth = srcWidth > 0 ? srcWidth : bmpWidth;
                int dstHeight = srcHeight > 0 ? srcHeight : bmpHeight;
                int srcWidthEffective = srcWidth > 0 ? srcWidth : dstWidth;
                if (outBuffer != null && dstWidth > 0 && dstHeight > 0)
                {
                    Bitmap frameBitmap = null;
                    try
                    {
                        frameBitmap = new Bitmap(dstWidth, dstHeight, pf);

                        // For 8bpp indexed bitmaps set grayscale palette
                        if (pf == PixelFormat.Format8bppIndexed)
                        {
                            var palette = frameBitmap.Palette;
                            for (int i = 0; i < palette.Entries.Length; i++)
                            {
                                palette.Entries[i] = Color.FromArgb(i, i, i);
                            }
                            frameBitmap.Palette = palette;
                        }

                        var bmpData = frameBitmap.LockBits(new Rectangle(0, 0, frameBitmap.Width, frameBitmap.Height), ImageLockMode.WriteOnly, frameBitmap.PixelFormat);

                        int bytesPerPixel = Image.GetPixelFormatSize(pf) / 8;
                        int srcRowBytes = srcWidthEffective * bytesPerPixel;
                        int dstStride = Math.Abs(bmpData.Stride);

                        if (dstStride == srcRowBytes && srcStride == srcRowBytes)
                        {
                            // Simple contiguous copy when no per-line padding and source stride matches
                            int copyBytes = Math.Min(outBuffer.Length, dstStride * dstHeight);
                            Marshal.Copy(outBuffer, 0, bmpData.Scan0, copyBytes);
                        }
                        else
                        {
                            // Copy line by line respecting source stride and destination stride (padding)
                            IntPtr scan0 = bmpData.Scan0;
                            for (int y = 0; y < dstHeight; y++)
                            {
                                int srcOffset = y * srcStride;
                                if (srcOffset >= outBuffer.Length) break;
                                int remaining = outBuffer.Length - srcOffset;
                                int copyLen = Math.Min(srcRowBytes, remaining);
                                copyLen = Math.Min(copyLen, Math.Abs(dstStride));
                                IntPtr destPtr = IntPtr.Add(scan0, y * bmpData.Stride);
                                Marshal.Copy(outBuffer, srcOffset, destPtr, copyLen);
                            }
                        }

                        frameBitmap.UnlockBits(bmpData);

                        // Store latest frame and schedule a single UI update that always picks the most recent frame.
                        lock (_latestFrameLock)
                        {
                            var prev = _latestFrame;
                            _latestFrame = frameBitmap; // take ownership
                            // dispose previous cached frame (not shown) to avoid memory leak
                            try { prev?.Dispose(); } catch { }
                        }

                        if (!_uiUpdatePending)
                        {
                            _uiUpdatePending = true;
                            if (picDisplay.InvokeRequired)
                            {
                                picDisplay.BeginInvoke(new Action(() =>
                                {
                                    Bitmap toShow = null;
                                    lock (_latestFrameLock)
                                    {
                                        toShow = _latestFrame;
                                        _latestFrame = null;
                                    }
                                    var old = picDisplay.Image;
                                    picDisplay.Image = toShow;
                                    old?.Dispose();
                                    _uiUpdatePending = false;
                                }));
                            }
                            else
                            {
                                Bitmap toShow = null;
                                lock (_latestFrameLock)
                                {
                                    toShow = _latestFrame;
                                    _latestFrame = null;
                                }
                                var old = picDisplay.Image;
                                picDisplay.Image = toShow;
                                old?.Dispose();
                                _uiUpdatePending = false;
                            }
                        }

                        // frameBitmap 已被 PictureBox 引用，不要在此处 Dispose
                        frameBitmap = null;
                    }
                    catch (Exception ex)
                    {
                        CameraLogger.Log($"Create/display frame bitmap failed: {ex.Message}");
                        frameBitmap?.Dispose();
                    }
                }

                _cameraManager.FreeImageBuffer(ref frameOut);
            }
        }

        private void CopyCImageData(CImage sourceImage, CImage targetImage)
        {
            try
            {
                if (sourceImage == null || targetImage == null) return;

                if (sourceImage.ImageData != null && sourceImage.ImageData.Length > 0)
                {
                    targetImage.ImageData = new byte[sourceImage.ImageData.Length];
                    Buffer.BlockCopy(sourceImage.ImageData, 0, targetImage.ImageData, 0, sourceImage.ImageData.Length);
                    targetImage.FrameLen = sourceImage.FrameLen;
                    return;
                }

                if (sourceImage.ImageAddr != IntPtr.Zero && sourceImage.FrameLen > 0)
                {
                    int len = checked((int)sourceImage.FrameLen);
                    targetImage.ImageData = new byte[len];
                    Marshal.Copy(sourceImage.ImageAddr, targetImage.ImageData, 0, len);
                    targetImage.FrameLen = sourceImage.FrameLen;
                    return;
                }

                throw new Exception("CImage contains no usable pixel data");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CopyCImageData failed: {ex.Message}");
                targetImage = null;
            }
        }

        private int InitDisplayBitmap()
        {
            try
            {
                int width = (int)_cameraManager.GetIntValue("Width");
                int height = (int)_cameraManager.GetIntValue("Height");
                MvGvspPixelType pixelFormat = _cameraManager.GetPixelFormat();

                if (IsMonoFormat(pixelFormat))
                {
                    _bitmapPixelFormat = PixelFormat.Format8bppIndexed;
                }
                else
                {
                    _bitmapPixelFormat = PixelFormat.Format24bppRgb;
                }

                _displayBitmap?.Dispose();
                _displayBitmap = new Bitmap(width, height, _bitmapPixelFormat);

                if (_bitmapPixelFormat == PixelFormat.Format8bppIndexed)
                {
                    var palette = _displayBitmap.Palette;
                    for (int i = 0; i < palette.Entries.Length; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    _displayBitmap.Palette = palette;
                }

                return CErrorDefine.MV_OK;
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"InitDisplayBitmap failed: {ex.Message}");
                ShowError($"初始化显示位图失败: {ex.Message}", 0);
                return -1;
            }
        }

        private bool IsMonoFormat(MvGvspPixelType pixelType)
        {
            return pixelType switch
            {
                MvGvspPixelType.PixelType_Gvsp_Mono8 or
                MvGvspPixelType.PixelType_Gvsp_Mono10 or
                MvGvspPixelType.PixelType_Gvsp_Mono10_Packed or
                MvGvspPixelType.PixelType_Gvsp_Mono12 or
                MvGvspPixelType.PixelType_Gvsp_Mono12_Packed or
                MvGvspPixelType.PixelType_Gvsp_Mono16
                => true,
                _ => false
            };
        }

        private void btnSaveImage_Click_1(object sender, EventArgs e)
        {
            LastFrameInfo snapshot = null;
            lock (_bufferLock)
            {
                if (_lastFrameInfo != null && _lastFrameInfo.Data != null && _lastFrameInfo.Data.Length > 0)
                {
                    snapshot = new LastFrameInfo
                    {
                        Data = _lastFrameInfo.Data,
                        Width = _lastFrameInfo.Width,
                        Height = _lastFrameInfo.Height,
                        PixelType = _lastFrameInfo.PixelType,
                        FrameLen = _lastFrameInfo.FrameLen
                    };
                }
            }

            if (snapshot == null)
            {
                ShowError("没有有效图像可保存", 0);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "BMP文件|*.bmp|JPG文件|*.jpg|PNG文件|*.png|TIFF文件|*.tif";
                if (sfd.ShowDialog() != DialogResult.OK) return;

                string filePath = sfd.FileName;
                try
                {
                    PixelFormat pf = (snapshot.PixelType == MvGvspPixelType.PixelType_Gvsp_Mono8) ? PixelFormat.Format8bppIndexed : PixelFormat.Format24bppRgb;

                    using (Bitmap bmp = new Bitmap(snapshot.Width, snapshot.Height, pf))
                    {
                        if (pf == PixelFormat.Format8bppIndexed)
                        {
                            var palette = bmp.Palette;
                            for (int i = 0; i < palette.Entries.Length; i++)
                            {
                                palette.Entries[i] = Color.FromArgb(i, i, i);
                            }
                            bmp.Palette = palette;
                        }

                        var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, pf);
                        try
                        {
                            int bytesPerPixel = Image.GetPixelFormatSize(pf) / 8;
                            int dstStride = Math.Abs(bmpData.Stride);
                            int srcRowBytes = snapshot.Width * bytesPerPixel;
                            int srcStride = snapshot.FrameLen > 0 ? Math.Max(1, snapshot.FrameLen / snapshot.Height) : srcRowBytes;

                            IntPtr scan0 = bmpData.Scan0;
                            for (int y = 0; y < snapshot.Height; y++)
                            {
                                int srcOffset = y * srcStride;
                                if (srcOffset >= snapshot.Data.Length) break;
                                int remaining = snapshot.Data.Length - srcOffset;
                                int copyLen = Math.Min(srcRowBytes, remaining);
                                copyLen = Math.Min(copyLen, dstStride);
                                IntPtr destPtr = IntPtr.Add(scan0, y * bmpData.Stride);
                                Marshal.Copy(snapshot.Data, srcOffset, destPtr, copyLen);
                            }
                        }
                        finally
                        {
                            bmp.UnlockBits(bmpData);
                        }

                        // Choose encoder by extension
                        string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                        switch (ext)
                        {
                            case ".jpg":
                            case ".jpeg":
                                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                break;
                            case ".png":
                                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                                break;
                            case ".tif":
                            case ".tiff":
                                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Tiff);
                                break;
                            default:
                                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                                break;
                        }
                    }

                    CameraLogger.Log($"Image saved to {filePath}");
                    MessageBox.Show($"图像已保存到: {filePath}", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SaveImage failed: {ex.Message}");
                    ShowError($"保存图像失败: {ex.Message}", 0);
                }
            }
        }

        private void btnStopCapture_Click(object sender, EventArgs e)
        {
            try
            {
                StopCaptureCommand();
                CameraLogger.Log("StopCapture command executed successfully");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"StopCapture failed: {ex.Message}");
                ShowError($"停止采集失败: {ex.Message}", 0);
            }
        }

        private void btnCloseCamera_Click(object sender, EventArgs e)
        {
            try
            {
                CloseCameraCommand();
                CameraLogger.Log("CloseCamera command executed successfully");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"CloseCamera failed: {ex.Message}");
                ShowError($"关闭相机失败: {ex.Message}", 0);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                EnumDevices();
                CameraLogger.Log("Refresh command executed successfully");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"Refresh failed: {ex.Message}");
                ShowError($"刷新设备列表失败: {ex.Message}", 0);
            }
        }

        private void ReadCameraParams()
        {
            try
            {
                if (!_cameraManager.IsConnected)
                {
                    return;
                }

                double exposureTime = _cameraManager.GetExposureTime();
                float gain = _cameraManager.GetGain();
                double frameRate = _cameraManager.GetFrameRate();
                int width = _cameraManager.GetWidth();
                int height = _cameraManager.GetHeight();

                CameraLogger.Log($"Camera parameters - Exposure: {exposureTime}us, Gain: {gain}dB, FrameRate: {frameRate}fps, Size: {width}x{height}");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"ReadCameraParams failed: {ex.Message}");
            }
        }

        private void UpdateCtrlState(bool isConnected)
        {
            btnOpenCamera.Enabled = !isConnected;
            btnCloseCamera.Enabled = isConnected;
            btnStartCapture.Enabled = isConnected && !_isGrabbing;
            btnStopCapture.Enabled = isConnected && _isGrabbing;
            btnSaveImage.Enabled = isConnected && !_isGrabbing;
            bnContinuesMode.Enabled = isConnected;
            bnTriggerMode.Enabled = isConnected;
        }

        private void ShowError(string message, int errorCode)
        {
            string fullMessage = errorCode != 0 ? $"{message} (错误码: 0x{errorCode:X})" : message;
            MessageBox.Show(fullMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            CameraLogger.Log($"Error: {fullMessage}");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (_isGrabbing)
                {
                    StopCaptureCommand();
                }

                if (_cameraManager.IsConnected)
                {
                    _cameraManager.CloseCamera();
                }

                _cameraManager?.Dispose();
                CameraLogger.Log("Form closing, camera resources released");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"Form closing error: {ex.Message}");
            }

            base.OnFormClosing(e);
        }

        private void btnSetParams_Click_1(object sender, EventArgs e)
        {
            try
            {
                if (!_cameraManager.IsConnected)
                {
                    ShowError("相机未连接", 0);
                    return;
                }

                if (double.TryParse(txtExposure.Text, out double exposureTime))
                {
                    _cameraManager.SetExposureTime(exposureTime);
                }

                if (float.TryParse(txtGain.Text, out float gain))
                {
                    _cameraManager.SetGain(gain);
                }

                if (double.TryParse(txtFrameRate.Text, out double frameRate))
                {
                    _cameraManager.SetFrameRate(frameRate);
                }

                CameraLogger.Log("Camera parameters set successfully");
                MessageBox.Show("参数设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"SetParams failed: {ex.Message}");
                ShowError($"设置参数失败: {ex.Message}", 0);
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (bnContinuesMode.Checked && _cameraManager.IsConnected)
                {
                    _cameraManager.SetAcquisitionMode(MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                    CameraLogger.Log("Acquisition mode set to Continuous");
                }
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"radioButton1_CheckedChanged failed: {ex.Message}");
                ShowError($"设置采集模式失败: {ex.Message}", 0);
            }
        }

        private void bnTriggerMode_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (bnTriggerMode.Checked && _cameraManager.IsConnected)
                {
                    _cameraManager.SetAcquisitionMode(MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_SINGLE);
                    CameraLogger.Log("Acquisition mode set to Trigger");
                }
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"bnTriggerMode_CheckedChanged failed: {ex.Message}");
                ShowError($"设置采集模式失败: {ex.Message}", 0);
            }
        }

        private void bnTriggerExec_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_cameraManager.IsConnected)
                {
                    ShowError("相机未连接", 0);
                    return;
                }

                _cameraManager.SetTriggerMode(MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                _cameraManager.SetTriggerSource(MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                _cameraManager.Camera.SetEnumValue("TriggerSoftware", 1);
                CameraLogger.Log("Software trigger executed");
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"bnTriggerExec_Click failed: {ex.Message}");
                ShowError($"软触发失败: {ex.Message}", 0);
            }
        }

        private void cbSoftTrigger_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (!_cameraManager.IsConnected)
                {
                    return;
                }

                if (cbSoftTrigger.Checked)
                {
                    _cameraManager.SetTriggerMode(MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                    _cameraManager.SetTriggerSource(MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                    CameraLogger.Log("Soft trigger enabled");
                }
                else
                {
                    _cameraManager.SetTriggerMode(MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
                    CameraLogger.Log("Soft trigger disabled");
                }
            }
            catch (Exception ex)
            {
                CameraLogger.Log($"cbSoftTrigger_CheckedChanged failed: {ex.Message}");
                ShowError($"设置软触发失败: {ex.Message}", 0);
            }
        }
    }
}
