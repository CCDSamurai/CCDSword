using System.ComponentModel;

namespace HikCameraCapture
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            cbDeviceList = new ComboBox();
            btnOpenCamera = new Button();
            btnCloseCamera = new Button();
            btnStartCapture = new Button();
            btnStopCapture = new Button();
            btnSetParams = new Button();
            btnSaveImage = new Button();
            txtExposure = new TextBox();
            txtGain = new TextBox();
            txtFrameRate = new TextBox();
            txtPixelFormat = new TextBox();
            picDisplay = new PictureBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            bnContinuesMode = new RadioButton();
            bnTriggerMode = new RadioButton();
            bnTriggerExec = new Button();
            cbSoftTrigger = new CheckBox();
            ((ISupportInitialize)picDisplay).BeginInit();
            SuspendLayout();
            // 
            // cbDeviceList
            // 
            cbDeviceList.FormattingEnabled = true;
            cbDeviceList.Location = new Point(366, 6);
            cbDeviceList.Name = "cbDeviceList";
            cbDeviceList.Size = new Size(121, 25);
            cbDeviceList.TabIndex = 0;
            // 
            // btnOpenCamera
            // 
            btnOpenCamera.Location = new Point(239, 350);
            btnOpenCamera.Name = "btnOpenCamera";
            btnOpenCamera.Size = new Size(121, 23);
            btnOpenCamera.TabIndex = 1;
            btnOpenCamera.Text = "btnOpenCamera";
            btnOpenCamera.UseVisualStyleBackColor = true;
            btnOpenCamera.Click += btnOpenCamera_Click_1;
            // 
            // btnCloseCamera
            // 
            btnCloseCamera.Location = new Point(366, 350);
            btnCloseCamera.Name = "btnCloseCamera";
            btnCloseCamera.Size = new Size(121, 23);
            btnCloseCamera.TabIndex = 2;
            btnCloseCamera.Text = "btnCloseCamera";
            btnCloseCamera.UseVisualStyleBackColor = true;
            btnCloseCamera.Click += btnCloseCamera_Click;
            // 
            // btnStartCapture
            // 
            btnStartCapture.Location = new Point(239, 379);
            btnStartCapture.Name = "btnStartCapture";
            btnStartCapture.Size = new Size(121, 23);
            btnStartCapture.TabIndex = 3;
            btnStartCapture.Text = "btnStartCapture";
            btnStartCapture.UseVisualStyleBackColor = true;
            btnStartCapture.Click += btnStartCapture_Click_1;
            // 
            // btnStopCapture
            // 
            btnStopCapture.Location = new Point(366, 379);
            btnStopCapture.Name = "btnStopCapture";
            btnStopCapture.Size = new Size(121, 23);
            btnStopCapture.TabIndex = 4;
            btnStopCapture.Text = "btnStopCapture";
            btnStopCapture.UseVisualStyleBackColor = true;
            btnStopCapture.Click += btnStopCapture_Click;
            // 
            // btnSetParams
            // 
            btnSetParams.Location = new Point(89, 317);
            btnSetParams.Name = "btnSetParams";
            btnSetParams.Size = new Size(100, 23);
            btnSetParams.TabIndex = 5;
            btnSetParams.Text = "btnSetParams";
            btnSetParams.UseVisualStyleBackColor = true;
            btnSetParams.Click += btnSetParams_Click_1;
            // 
            // btnSaveImage
            // 
            btnSaveImage.Location = new Point(89, 346);
            btnSaveImage.Name = "btnSaveImage";
            btnSaveImage.Size = new Size(100, 23);
            btnSaveImage.TabIndex = 6;
            btnSaveImage.Text = "btnSaveImage";
            btnSaveImage.UseVisualStyleBackColor = true;
            btnSaveImage.Click += btnSaveImage_Click_1;
            // 
            // txtExposure
            // 
            txtExposure.Location = new Point(89, 197);
            txtExposure.Name = "txtExposure";
            txtExposure.Size = new Size(100, 23);
            txtExposure.TabIndex = 7;
            // 
            // txtGain
            // 
            txtGain.Location = new Point(89, 226);
            txtGain.Name = "txtGain";
            txtGain.Size = new Size(100, 23);
            txtGain.TabIndex = 8;
            // 
            // txtFrameRate
            // 
            txtFrameRate.Location = new Point(89, 259);
            txtFrameRate.Name = "txtFrameRate";
            txtFrameRate.Size = new Size(100, 23);
            txtFrameRate.TabIndex = 9;
            // 
            // txtPixelFormat
            // 
            txtPixelFormat.Location = new Point(89, 288);
            txtPixelFormat.Name = "txtPixelFormat";
            txtPixelFormat.Size = new Size(100, 23);
            txtPixelFormat.TabIndex = 10;
            // 
            // picDisplay
            // 
            picDisplay.Location = new Point(239, 37);
            picDisplay.Name = "picDisplay";
            picDisplay.Size = new Size(363, 274);
            picDisplay.SizeMode = PictureBoxSizeMode.Zoom;
            picDisplay.TabIndex = 11;
            picDisplay.TabStop = false;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(27, 200);
            label1.Name = "label1";
            label1.Size = new Size(56, 17);
            label1.TabIndex = 12;
            label1.Text = "曝光时间";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(51, 232);
            label2.Name = "label2";
            label2.Size = new Size(32, 17);
            label2.TabIndex = 13;
            label2.Text = "增益";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(51, 265);
            label3.Name = "label3";
            label3.Size = new Size(32, 17);
            label3.TabIndex = 14;
            label3.Text = "帧率";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(3, 294);
            label4.Name = "label4";
            label4.Size = new Size(80, 17);
            label4.TabIndex = 15;
            label4.Text = "像素格式显示";
            // 
            // bnContinuesMode
            // 
            bnContinuesMode.AutoSize = true;
            bnContinuesMode.Enabled = false;
            bnContinuesMode.Location = new Point(239, 317);
            bnContinuesMode.Name = "bnContinuesMode";
            bnContinuesMode.Size = new Size(74, 21);
            bnContinuesMode.TabIndex = 16;
            bnContinuesMode.TabStop = true;
            bnContinuesMode.Text = "连续模式";
            bnContinuesMode.UseVisualStyleBackColor = true;
            bnContinuesMode.CheckedChanged += radioButton1_CheckedChanged;
            // 
            // bnTriggerMode
            // 
            bnTriggerMode.AutoSize = true;
            bnTriggerMode.Enabled = false;
            bnTriggerMode.Location = new Point(366, 317);
            bnTriggerMode.Name = "bnTriggerMode";
            bnTriggerMode.Size = new Size(74, 21);
            bnTriggerMode.TabIndex = 17;
            bnTriggerMode.TabStop = true;
            bnTriggerMode.Text = "触发模式";
            bnTriggerMode.UseVisualStyleBackColor = true;
            bnTriggerMode.CheckedChanged += bnTriggerMode_CheckedChanged;
            // 
            // bnTriggerExec
            // 
            bnTriggerExec.Location = new Point(366, 411);
            bnTriggerExec.Name = "bnTriggerExec";
            bnTriggerExec.Size = new Size(121, 23);
            bnTriggerExec.TabIndex = 18;
            bnTriggerExec.Text = "单击软触发";
            bnTriggerExec.UseVisualStyleBackColor = true;
            bnTriggerExec.Click += bnTriggerExec_Click;
            // 
            // cbSoftTrigger
            // 
            cbSoftTrigger.AutoSize = true;
            cbSoftTrigger.Location = new Point(297, 413);
            cbSoftTrigger.Name = "cbSoftTrigger";
            cbSoftTrigger.Size = new Size(63, 21);
            cbSoftTrigger.TabIndex = 19;
            cbSoftTrigger.Text = "软触发";
            cbSoftTrigger.UseVisualStyleBackColor = true;
            cbSoftTrigger.CheckedChanged += cbSoftTrigger_CheckedChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(634, 441);
            Controls.Add(cbSoftTrigger);
            Controls.Add(bnTriggerExec);
            Controls.Add(bnTriggerMode);
            Controls.Add(bnContinuesMode);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(picDisplay);
            Controls.Add(txtPixelFormat);
            Controls.Add(txtFrameRate);
            Controls.Add(txtGain);
            Controls.Add(txtExposure);
            Controls.Add(btnSaveImage);
            Controls.Add(btnSetParams);
            Controls.Add(btnStopCapture);
            Controls.Add(btnStartCapture);
            Controls.Add(btnCloseCamera);
            Controls.Add(btnOpenCamera);
            Controls.Add(cbDeviceList);
            Name = "Form1";
            Text = "Form1";
            ((ISupportInitialize)picDisplay).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox cbDeviceList;
        private Button btnOpenCamera;
       
        private Button btnCloseCamera;
        private Button btnStartCapture;
        private Button btnStopCapture;
        private Button btnSetParams;
        private Button btnSaveImage;
        private TextBox txtExposure;
        private TextBox txtGain;
        private TextBox txtFrameRate;
        private TextBox txtPixelFormat;
        private PictureBox picDisplay;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private RadioButton bnContinuesMode;
        private RadioButton bnTriggerMode;
        private Button bnTriggerExec;
        private CheckBox cbSoftTrigger;
    }
}
