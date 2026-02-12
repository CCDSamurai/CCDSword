using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;
using System;
using System.Collections.Generic;
using System.Text;
namespace HikCameraCapture
{
    public class CameraManager : IDisposable
    {
        private CCamera _camera;
        private List<CCameraInfo> _deviceList;
        private bool _isInitialized;
        private bool _isConnected;
        private readonly object _lockObject = new object();

        public bool IsInitialized => _isInitialized;
        public bool IsConnected => _isConnected;
        public List<CCameraInfo> DeviceList => _deviceList;
        public CCamera Camera => _camera;

        public CameraManager()
        {
            _camera = new CCamera();
            _deviceList = new List<CCameraInfo>();
            _isInitialized = false;
            _isConnected = false;
            CameraLogger.Log("CameraManager initialized");
        }

        public void Initialize()
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log("Starting camera initialization");
                    _deviceList.Clear();
                    GC.Collect();

                    int ret = CSystem.EnumDevices(CSystem.MV_GIGE_DEVICE | CSystem.MV_USB_DEVICE, ref _deviceList);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"EnumDevices failed with error code: 0x{ret:X}");
                        throw new CameraException($"枚举设备失败，错误码: 0x{ret:X}");
                    }

                    _isInitialized = true;
                    CameraLogger.Log($"Camera initialization completed. Found {_deviceList.Count} device(s)");

                    for (int i = 0; i < _deviceList.Count; i++)
                    {
                        var dev = _deviceList[i];
                        if (dev.nTLayerType == CSystem.MV_GIGE_DEVICE)
                        {
                            var gigeInfo = (CGigECameraInfo)dev;
                            CameraLogger.Log($"Device {i}: GEV - {gigeInfo.UserDefinedName ?? gigeInfo.chModelName} (SN: {gigeInfo.chSerialNumber})");
                        }
                        else if (dev.nTLayerType == CSystem.MV_USB_DEVICE)
                        {
                            var usbInfo = (CUSBCameraInfo)dev;
                            CameraLogger.Log($"Device {i}: U3V - {usbInfo.UserDefinedName ?? usbInfo.chModelName} (SN: {usbInfo.chSerialNumber})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"Camera initialization failed: {ex.Message}");
                    throw new CameraException($"相机初始化失败: {ex.Message}", ex);
                }
            }
        }

        public void OpenCamera(int deviceIndex)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Opening camera at index {deviceIndex}");

                    if (!_isInitialized)
                    {
                        CameraLogger.Log("Camera not initialized, initializing now");
                        Initialize();
                    }

                    if (_deviceList.Count == 0 || deviceIndex < 0 || deviceIndex >= _deviceList.Count)
                    {
                        CameraLogger.Log($"Invalid device index: {deviceIndex}. Total devices: {_deviceList.Count}");
                        throw new CameraException("无效的设备索引");
                    }

                    var selectedDev = _deviceList[deviceIndex];
                    CameraLogger.Log($"Creating handle for device at index {deviceIndex}");

                    int ret = _camera.CreateHandle(ref selectedDev);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"CreateHandle failed with error code: 0x{ret:X}");
                        throw new CameraException($"创建相机句柄失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log("Opening device");
                    ret = _camera.OpenDevice();
                    if (ret != CErrorDefine.MV_OK)
                    {
                        _camera.DestroyHandle();
                        CameraLogger.Log($"OpenDevice failed with error code: 0x{ret:X}");
                        throw new CameraException($"打开相机失败，错误码: 0x{ret:X}");
                    }

                    _isConnected = true;
                    CameraLogger.Log("Camera opened successfully");

                    if (selectedDev.nTLayerType == CSystem.MV_GIGE_DEVICE)
                    {
                        CameraLogger.Log("Configuring GigE packet size");
                        int packetSize = _camera.GIGE_GetOptimalPacketSize();
                        if (packetSize > 0)
                        {
                            ret = _camera.SetIntValue("GevSCPSPacketSize", (uint)packetSize);
                            if (ret != CErrorDefine.MV_OK)
                            {
                                CameraLogger.Log($"Set GevSCPSPacketSize failed with error code: 0x{ret:X}");
                                throw new CameraException($"设置数据包大小失败，错误码: 0x{ret:X}");
                            }
                            CameraLogger.Log($"GigE packet size set to {packetSize}");
                        }
                        else
                        {
                            CameraLogger.Log($"GetOptimalPacketSize returned invalid value: {packetSize}");
                            throw new CameraException($"获取最优数据包大小失败");
                        }
                    }

                    SetAcquisitionMode(MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                    SetTriggerMode(MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

                    CameraLogger.Log("Camera configuration completed");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"OpenCamera failed: {ex.Message}");
                    throw new CameraException($"打开相机失败: {ex.Message}", ex);
                }
            }
        }

        public void CloseCamera()
        {
            lock (_lockObject)
            {
                try
                {
                    if (_isConnected)
                    {
                        CameraLogger.Log("Closing camera");

                        try
                        {
                            _camera.StopGrabbing();
                            CameraLogger.Log("Grabbing stopped");
                        }
                        catch { }

                        _camera.CloseDevice();
                        _camera.DestroyHandle();
                        _isConnected = false;

                        CameraLogger.Log("Camera closed successfully");
                    }
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"CloseCamera failed: {ex.Message}");
                    throw new CameraException($"关闭相机失败: {ex.Message}", ex);
                }
            }
        }

        public void SetExposureTime(double exposureTime)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting exposure time to {exposureTime} us");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetFloatValue("ExposureTime", (float)exposureTime);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set ExposureTime failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置曝光时间失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Exposure time set to {exposureTime} us successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetExposureTime failed: {ex.Message}");
                    throw new CameraException($"设置曝光时间失败: {ex.Message}", ex);
                }
            }
        }

        public double GetExposureTime()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CFloatValue exposureTime = new CFloatValue();
                    int ret = _camera.GetFloatValue("ExposureTime", ref exposureTime);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get ExposureTime failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取曝光时间失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current exposure time: {exposureTime.CurValue} us");
                    return exposureTime.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetExposureTime failed: {ex.Message}");
                    throw new CameraException($"获取曝光时间失败: {ex.Message}", ex);
                }
            }
        }

        public void SetGain(float gain)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting gain to {gain} dB");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetFloatValue("Gain", gain);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set Gain failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置增益失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Gain set to {gain} dB successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetGain failed: {ex.Message}");
                    throw new CameraException($"设置增益失败: {ex.Message}", ex);
                }
            }
        }

        public float GetGain()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CFloatValue gain = new CFloatValue();
                    int ret = _camera.GetFloatValue("Gain", ref gain);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get Gain failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取增益失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current gain: {gain.CurValue} dB");
                    return gain.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetGain failed: {ex.Message}");
                    throw new CameraException($"获取增益失败: {ex.Message}", ex);
                }
            }
        }

        public void SetFrameRate(double frameRate)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting frame rate to {frameRate} fps");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetFloatValue("AcquisitionFrameRate", (float)frameRate);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set AcquisitionFrameRate failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置帧率失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Frame rate set to {frameRate} fps successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetFrameRate failed: {ex.Message}");
                    throw new CameraException($"设置帧率失败: {ex.Message}", ex);
                }
            }
        }

        public double GetFrameRate()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CFloatValue frameRate = new CFloatValue();
                    int ret = _camera.GetFloatValue("AcquisitionFrameRate", ref frameRate);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get AcquisitionFrameRate failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取帧率失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current frame rate: {frameRate.CurValue} fps");
                    return frameRate.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetFrameRate failed: {ex.Message}");
                    throw new CameraException($"获取帧率失败: {ex.Message}", ex);
                }
            }
        }

        public void SetWidth(int width)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting width to {width}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetIntValue("Width", (uint)width);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set Width failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置宽度失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Width set to {width} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetWidth failed: {ex.Message}");
                    throw new CameraException($"设置宽度失败: {ex.Message}", ex);
                }
            }
        }

        public int GetWidth()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CIntValue width = new CIntValue();
                    int ret = _camera.GetIntValue("Width", ref width);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get Width failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取宽度失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current width: {width.CurValue}");
                    return (int)width.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetWidth failed: {ex.Message}");
                    throw new CameraException($"获取宽度失败: {ex.Message}", ex);
                }
            }
        }

        public void SetHeight(int height)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting height to {height}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetIntValue("Height", (uint)height);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set Height failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置高度失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Height set to {height} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetHeight failed: {ex.Message}");
                    throw new CameraException($"设置高度失败: {ex.Message}", ex);
                }
            }
        }

        public int GetHeight()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CIntValue height = new CIntValue();
                    int ret = _camera.GetIntValue("Height", ref height);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get Height failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取高度失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current height: {height.CurValue}");
                    return (int)height.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetHeight failed: {ex.Message}");
                    throw new CameraException($"获取高度失败: {ex.Message}", ex);
                }
            }
        }

        public void SetPixelFormat(MvGvspPixelType pixelFormat)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting pixel format to {pixelFormat}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetEnumValue("PixelFormat", (uint)pixelFormat);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set PixelFormat failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置像素格式失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Pixel format set to {pixelFormat} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetPixelFormat failed: {ex.Message}");
                    throw new CameraException($"设置像素格式失败: {ex.Message}", ex);
                }
            }
        }

        public MvGvspPixelType GetPixelFormat()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CEnumValue pixelFormat = new CEnumValue();
                    int ret = _camera.GetEnumValue("PixelFormat", ref pixelFormat);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get PixelFormat failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取像素格式失败，错误码: 0x{ret:X}");
                    }

                    MvGvspPixelType format = (MvGvspPixelType)pixelFormat.CurValue;
                    CameraLogger.Log($"Current pixel format: {format}");
                    return format;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetPixelFormat failed: {ex.Message}");
                    throw new CameraException($"获取像素格式失败: {ex.Message}", ex);
                }
            }
        }

        public void SetAcquisitionMode(MV_CAM_ACQUISITION_MODE mode)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting acquisition mode to {mode}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetEnumValue("AcquisitionMode", (uint)mode);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set AcquisitionMode failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置采集模式失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Acquisition mode set to {mode} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetAcquisitionMode failed: {ex.Message}");
                    throw new CameraException($"设置采集模式失败: {ex.Message}", ex);
                }
            }
        }

        public void SetTriggerMode(MV_CAM_TRIGGER_MODE mode)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting trigger mode to {mode}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetEnumValue("TriggerMode", (uint)mode);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set TriggerMode failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置触发模式失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Trigger mode set to {mode} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetTriggerMode failed: {ex.Message}");
                    throw new CameraException($"设置触发模式失败: {ex.Message}", ex);
                }
            }
        }

        public void SetTriggerSource(MV_CAM_TRIGGER_SOURCE source)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting trigger source to {source}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetEnumValue("TriggerSource", (uint)source);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set TriggerSource failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置触发源失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Trigger source set to {source} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetTriggerSource failed: {ex.Message}");
                    throw new CameraException($"设置触发源失败: {ex.Message}", ex);
                }
            }
        }

        public void SetIntValue(string key, uint value)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting {key} to {value}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetIntValue(key, value);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set {key} failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置{key}失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"{key} set to {value} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetIntValue failed: {ex.Message}");
                    throw new CameraException($"设置{key}失败: {ex.Message}", ex);
                }
            }
        }

        public uint GetIntValue(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CIntValue value = new CIntValue();
                    int ret = _camera.GetIntValue(key, ref value);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get {key} failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取{key}失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current {key}: {value.CurValue}");
                    return (uint)value.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetIntValue failed: {ex.Message}");
                    throw new CameraException($"获取{key}失败: {ex.Message}", ex);
                }
            }
        }

        public void SetFloatValue(string key, float value)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting {key} to {value}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetFloatValue(key, value);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set {key} failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置{key}失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"{key} set to {value} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetFloatValue failed: {ex.Message}");
                    throw new CameraException($"设置{key}失败: {ex.Message}", ex);
                }
            }
        }

        public float GetFloatValue(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CFloatValue value = new CFloatValue();
                    int ret = _camera.GetFloatValue(key, ref value);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get {key} failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取{key}失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current {key}: {value.CurValue}");
                    return value.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetFloatValue failed: {ex.Message}");
                    throw new CameraException($"获取{key}失败: {ex.Message}", ex);
                }
            }
        }

        public void SetEnumValue(string key, uint value)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Setting {key} to {value}");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.SetEnumValue(key, value);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Set {key} failed with error code: 0x{ret:X}");
                        throw new CameraException($"设置{key}失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"{key} set to {value} successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SetEnumValue failed: {ex.Message}");
                    throw new CameraException($"设置{key}失败: {ex.Message}", ex);
                }
            }
        }

        public uint GetEnumValue(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    CEnumValue value = new CEnumValue();
                    int ret = _camera.GetEnumValue(key, ref value);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"Get {key} failed with error code: 0x{ret:X}");
                        throw new CameraException($"获取{key}失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Current {key}: {value.CurValue}");
                    return value.CurValue;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetEnumValue failed: {ex.Message}");
                    throw new CameraException($"获取{key}失败: {ex.Message}", ex);
                }
            }
        }

        public void StartGrabbing()
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log("Starting grabbing");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.StartGrabbing();
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"StartGrabbing failed with error code: 0x{ret:X}");
                        throw new CameraException($"开始采集失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log("Grabbing started successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"StartGrabbing failed: {ex.Message}");
                    throw new CameraException($"开始采集失败: {ex.Message}", ex);
                }
            }
        }

        public void StopGrabbing()
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log("Stopping grabbing");

                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.StopGrabbing();
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"StopGrabbing failed with error code: 0x{ret:X}");
                        throw new CameraException($"停止采集失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log("Grabbing stopped successfully");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"StopGrabbing failed: {ex.Message}");
                    throw new CameraException($"停止采集失败: {ex.Message}", ex);
                }
            }
        }

        public int GetImageBuffer(ref CFrameout frameOut, int timeoutMs)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    int ret = _camera.GetImageBuffer(ref frameOut, timeoutMs);
                    return ret;
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"GetImageBuffer failed: {ex.Message}");
                    throw new CameraException($"获取图像缓冲失败: {ex.Message}", ex);
                }
            }
        }

        public void FreeImageBuffer(ref CFrameout frameOut)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    _camera.FreeImageBuffer(ref frameOut);
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"FreeImageBuffer failed: {ex.Message}");
                    throw new CameraException($"释放图像缓冲失败: {ex.Message}", ex);
                }
            }
        }

        public void ConvertPixelType(ref CPixelConvertParam convertParam)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_isConnected)
                    {
                        CameraLogger.Log("Camera not connected");
                        throw new CameraException("相机未连接");
                    }

                    _camera.ConvertPixelType(ref convertParam);
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"ConvertPixelType failed: {ex.Message}");
                    throw new CameraException($"像素格式转换失败: {ex.Message}", ex);
                }
            }
        }

        public void SaveImage(CImage image, string filePath, MV_SAVE_IAMGE_TYPE imageType = MV_SAVE_IAMGE_TYPE.MV_IMAGE_BMP)
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log($"Saving image to {filePath} with type {imageType}");

                    if (image == null)
                    {
                        CameraLogger.Log("Image is null");
                        throw new CameraException("图像为空");
                    }

                    if (string.IsNullOrEmpty(filePath))
                    {
                        CameraLogger.Log("File path is empty");
                        throw new CameraException("文件路径为空");
                    }

                    CSaveImgToFileParam saveParam = new CSaveImgToFileParam();
                    saveParam.Image = image;
                    saveParam.MethodValue = 2;
                    saveParam.ImagePath = filePath;
                    saveParam.ImageType = imageType;

                    int ret = _camera.SaveImageToFile(ref saveParam);
                    if (ret != CErrorDefine.MV_OK)
                    {
                        CameraLogger.Log($"SaveImageToFile failed with error code: 0x{ret:X}");
                        throw new CameraException($"保存图像失败，错误码: 0x{ret:X}");
                    }

                    CameraLogger.Log($"Image saved successfully to {filePath}");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"SaveImage failed: {ex.Message}");
                    throw new CameraException($"保存图像失败: {ex.Message}", ex);
                }
            }
        }

        public void SaveImage(CImage image, string filePath, string fileExtension)
        {
            MV_SAVE_IAMGE_TYPE imageType = GetImageTypeFromExtension(fileExtension);
            SaveImage(image, filePath, imageType);
        }

        public void SaveImageAsBmp(CImage image, string filePath)
        {
            SaveImage(image, filePath, MV_SAVE_IAMGE_TYPE.MV_IMAGE_BMP);
        }

        public void SaveImageAsJpeg(CImage image, string filePath)
        {
            SaveImage(image, filePath, MV_SAVE_IAMGE_TYPE.MV_IMAGE_JPEG);
        }

        public void SaveImageAsPng(CImage image, string filePath)
        {
            SaveImage(image, filePath, MV_SAVE_IAMGE_TYPE.MV_IMAGE_PNG);
        }

        public void SaveImageAsTiff(CImage image, string filePath)
        {
            SaveImage(image, filePath, MV_SAVE_IAMGE_TYPE.MV_IMAGE_TIF);
        }

        private MV_SAVE_IAMGE_TYPE GetImageTypeFromExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return MV_SAVE_IAMGE_TYPE.MV_IMAGE_BMP;
            }

            string ext = extension.ToLower().TrimStart('.');
            return ext switch
            {
                "bmp" => MV_SAVE_IAMGE_TYPE.MV_IMAGE_BMP,
                "jpg" or "jpeg" => MV_SAVE_IAMGE_TYPE.MV_IMAGE_JPEG,
                "png" => MV_SAVE_IAMGE_TYPE.MV_IMAGE_PNG,
                "tif" or "tiff" => MV_SAVE_IAMGE_TYPE.MV_IMAGE_TIF,
                _ => MV_SAVE_IAMGE_TYPE.MV_IMAGE_BMP
            };
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                try
                {
                    CameraLogger.Log("Disposing CameraManager");

                    if (_isConnected)
                    {
                        CloseCamera();
                    }

                    _isInitialized = false;
                    CameraLogger.Log("CameraManager disposed");
                }
                catch (Exception ex)
                {
                    CameraLogger.Log($"Dispose failed: {ex.Message}");
                }
            }
        }
    }

    public class CameraException : Exception
    {
        public CameraException(string message) : base(message)
        {
        }

        public CameraException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
