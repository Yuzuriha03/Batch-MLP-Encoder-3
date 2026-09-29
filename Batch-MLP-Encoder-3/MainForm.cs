using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using MediaInfoLib;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Threading;
using System.Globalization;

namespace SadPencil.BatchMLPEncoder3 {

    public partial class MainForm : Form {
        public const string Version = "4.0";
        private readonly BatchCommandLineOptions BatchOptions;

        public MainForm() {
            //Thread.CurrentThread.CurrentUICulture =new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
        }

        internal MainForm(BatchCommandLineOptions options) {
            this.BatchOptions = options;
            InitializeComponent();
        }

        private string[,] Files;
        //EncodeNames[i] 是实际交给 eac3to / Surcode 的文件名（不含扩展名）。
        //当 Files[i, 0] 含有 Surcode MLP Encoder 无法处理的字符（如韩文）时，它是临时的纯 ASCII 文件名。
        private string[] EncodeNames;
        //NameReplaced[i] 表示 EncodeNames[i] 是临时名，MLP 生成后需要改回 Files[i, 0]。
        private bool[] NameReplaced;
        //SurcodeSucceeded[i] 表示本次批处理中第 i 个文件确实成功产出了 MLP。
        //只有它和 NameReplaced[i] 同时为 true 时才会在最后统一改名。
        private bool[] SurcodeSucceeded;
        //最后统一改名时有多少个文件改名失败（音频已正确，只是名字没改回来）
        private int RenameFailureCount;
        //本次批处理是否已提示过“选了 20 bit，但 eac3to 实际写入 24 bit 容器”
        private bool Rebit20BitWarned;
        //删除残留文件时最多等待多久（毫秒）
        private const int ShortFileWaitMilliseconds = 3000;
        //最后统一改名时每个文件最多等待多久（毫秒）。
        //改名发生在所有文件编码完之后，Surcode 早已退出、句柄已释放，
        //剩下的占用者只可能是外部程序（播放器、杀毒软件），等太久没意义。
        private const int RenameWaitMilliseconds = 5000;
        //文件重试的间隔（毫秒）
        private const int FileRetryIntervalMilliseconds = 250;
        private bool _Processing;
        private bool[] eac3toOK, eac3toFailed, SurcodeFailed;
        private bool Processing {
            get {
                return this._Processing;
            }
            set {
                this._Processing = value;
                this.UpdateMainFormText();
            }

        }

        private void UpdateMainFormText() {
            if (this.Processing) {
                this.Text = "Batch MLP Encoder - " + MainFormCodeStrings.SetMainFormTextString;
            }
            else {
                this.Text = "Batch MLP Encoder - Ver." + Version;
            }
        }

        private bool eac3Processing, SurcodeProcessing;
        private bool Page1PictureBoxClicked = false;

        private class Page6WorkerReportArgument {
            public enum Orders {
                Add,
                MessageBox
            }
            private Orders _Order;
            private string _Message;
            private string _FileName;
            private string _Time;
            private bool _Stress;

            public bool Stress {
                get {
                    return this._Stress;
                }
                set {
                    this._Stress = value;
                }
            }

            public Page6WorkerReportArgument(Orders Order, string Message, string FileName, bool Stress) {
                this._Order = Order;
                this._Message = Message;
                this._FileName = FileName;
                this._Time = DateTime.Now.ToLongTimeString();
                this._Stress = Stress;
            }
            public Orders Order {
                get {
                    return this._Order;
                }
                set {
                    this._Order = value;
                }
            }

            public string FileName {
                get {
                    return this._FileName;
                }
                set {
                    this._FileName = value;
                }
            }

            public string Message {
                get {
                    return this._Message;
                }
                set {
                    this._Message = value;
                }
            }
            public string Time {
                get {
                    return this._Time;
                }
                set {
                    this._Time = value;
                }
            }

        }

        private void LoadSettings() {
            Debug.WriteLine("Load Settings...");
            //  if (!String.IsNullOrWhiteSpace(Properties.Settings.Default.SurcodeExeFullname)) {
            if (System.IO.File.Exists(Properties.Settings.Default.SurcodeExeFullname)) {
                this.Page1SurcodeTextBox.Text = Properties.Settings.Default.SurcodeExeFullname;
            }
            //     }
            if (System.IO.File.Exists(Properties.Settings.Default.Eac3toExeFullname)) {
                this.Page1eac3toTextbox.Text = Properties.Settings.Default.Eac3toExeFullname;
            }

            if (System.IO.Directory.Exists(Properties.Settings.Default.TempFolderPath)) {
                this.Page5TempTextbox.Text = Properties.Settings.Default.TempFolderPath;
            }

            if (System.IO.Directory.Exists(Properties.Settings.Default.OutputFolderPath)) {
                this.Page5SaveTextbox.Text = Properties.Settings.Default.OutputFolderPath;
            }

            //    if (System.IO.File.Exists(Properties.Settings.Default.SURCODEFULLNAME)) Page1SurcodeTextBox.Text = Properties.Settings.Default.SURCODEFULLNAME;
            //    if (System.IO.File.Exists(Properties.Settings.Default.EAC3TOFULLNAME)) Page1eac3toTextbox.Text = Properties.Settings.Default.EAC3TOFULLNAME;
            //    if (System.IO.Directory.Exists(Properties.Settings.Default.TEMPPATH)) Page5TempTextbox.Text = Properties.Settings.Default.TEMPPATH;
            //    if (System.IO.Directory.Exists(Properties.Settings.Default.SAVEPATH)) Page5SaveTextbox.Text = Properties.Settings.Default.SAVEPATH;

            //    Page5AutoCleanCheckbox.Checked = Properties.Settings.Default.AUTOCLEAN;

            //    if (Properties.Settings.Default.A > 0 & Properties.Settings.Default.A < 10000) Page4A.Value = Properties.Settings.Default.A;
            //    if (Properties.Settings.Default.B > 0 & Properties.Settings.Default.B < 10000) Page4B.Value = Properties.Settings.Default.B;
            //    if (Properties.Settings.Default.C > 0 & Properties.Settings.Default.C < 10000) Page4C.Value = Properties.Settings.Default.C;
            //    if (Properties.Settings.Default.D > 0 & Properties.Settings.Default.D < 10000) Page4D.Value = Properties.Settings.Default.D;
            //    if (Properties.Settings.Default.E > 0 & Properties.Settings.Default.E < 10000) Page4E.Value = Properties.Settings.Default.E;
            //    if (Properties.Settings.Default.F > 0 & Properties.Settings.Default.F < 10000) Page4F.Value = Properties.Settings.Default.F;

        }

        private void SaveSettings() {
            Debug.WriteLine("Save Settings...");
            //    if (System.IO.File.Exists(Page1SurcodeTextBox.Text)) Properties.Settings.Default.SURCODEFULLNAME = Page1SurcodeTextBox.Text;
            //    if (System.IO.File.Exists(Page1eac3toTextbox.Text)) Properties.Settings.Default.EAC3TOFULLNAME = Page1eac3toTextbox.Text;
            //    if (System.IO.Directory.Exists(Page5TempTextbox.Text)) Properties.Settings.Default.TEMPPATH = Page5TempTextbox.Text;
            //    if (System.IO.Directory.Exists(Page5SaveTextbox.Text)) Properties.Settings.Default.SAVEPATH = Page5SaveTextbox.Text;
            if (System.IO.File.Exists(this.Page1SurcodeTextBox.Text)) {
                Properties.Settings.Default.SurcodeExeFullname = this.Page1SurcodeTextBox.Text;
            }
            //     }
            if (System.IO.File.Exists(this.Page1eac3toTextbox.Text)) {
                Properties.Settings.Default.Eac3toExeFullname = this.Page1eac3toTextbox.Text;
            }

            if (System.IO.Directory.Exists(this.Page5TempTextbox.Text)) {
                Properties.Settings.Default.TempFolderPath = this.Page5TempTextbox.Text;
            }

            if (System.IO.Directory.Exists(this.Page5SaveTextbox.Text)) {
                Properties.Settings.Default.OutputFolderPath = this.Page5SaveTextbox.Text;
            }
            //    Properties.Settings.Default.A = Page4A.Value;
            //    Properties.Settings.Default.B = Page4B.Value;
            //    Properties.Settings.Default.C = Page4C.Value;
            //    Properties.Settings.Default.D = Page4D.Value;
            //    Properties.Settings.Default.E = Page4E.Value;
            //    Properties.Settings.Default.F = Page4F.Value;
            //    Properties.Settings.Default.AUTOCLEAN = Page5AutoCleanCheckbox.Checked;

            Properties.Settings.Default.Save();
        }

        private void MainForm_Load(object sender, EventArgs e) {
            this.UpdateMainFormText();
            this.VersionLabel.Text = "Version " + Version;

            LoadSettings();

            if (this.BatchOptions != null) {
                if (!String.IsNullOrWhiteSpace(this.BatchOptions.SurcodePath)) {
                    this.Page1SurcodeTextBox.Text = this.BatchOptions.SurcodePath;
                }
                if (!String.IsNullOrWhiteSpace(this.BatchOptions.Eac3toPath)) {
                    this.Page1eac3toTextbox.Text = this.BatchOptions.Eac3toPath;
                }
                this.Page5TempTextbox.Text = this.BatchOptions.TempDirectory;
                this.Page5SaveTextbox.Text = this.BatchOptions.OutputDirectory;
                this.Page5AutoCleanCheckbox.Checked = true;
                this.ApplyBatchAudioOptions();
                foreach (string file in this.BatchOptions.InputFiles) {
                    AddFile(file);
                }
                this.BeginInvoke(new Action(delegate {
                    if (this.Page2ListView.Items.Count != this.BatchOptions.InputFiles.Count) {
                        Environment.ExitCode = 3;
                        this.Close();
                        return;
                    }
                    this.Page5StartButton.PerformClick();
                }));
            }

        }

        private void ApplyBatchAudioOptions() {
            this.Page3AlwaysResampleRadioButton.Checked = true;
            this.Page3_44100RadioButton.Checked = this.BatchOptions.SampleRate == 44100;
            this.Page3_48000RadioButton.Checked = this.BatchOptions.SampleRate == 48000;
            this.Page3_88200RadioButton.Checked = this.BatchOptions.SampleRate == 88200;
            this.Page3_96000RadioButton.Checked = this.BatchOptions.SampleRate == 96000;
            this.Page3_176400RadioButton.Checked = this.BatchOptions.SampleRate == 176400;
            this.Page3_192000RadioButton.Checked = this.BatchOptions.SampleRate == 192000;
            this.Page3AlwaysRebitRadioButton.Checked = true;
            this.Page3_16RadioButton.Checked = this.BatchOptions.Bits == 16;
            this.Page3_20RadioButton.Checked = this.BatchOptions.Bits == 20;
            this.Page3_24RadioButton.Checked = this.BatchOptions.Bits == 24;
        }

        private void Page1PictureBox_Click(object sender, EventArgs e) {

            this.Page1PictureBoxClicked = !this.Page1PictureBoxClicked;
            if (this.Page1PictureBoxClicked)
                this.Page1PictureBox.Image = Properties.Resources.audiodvd256;
            else
                this.Page1PictureBox.Image = Properties.Resources.burnCD256;
        }

        private void PagesNextButton_Click(object sender, EventArgs e) {
            this.MainTabControl.SelectedIndex += 1;
        }

        private void PagesBackButton_Click(object sender, EventArgs e) {
            this.MainTabControl.SelectedIndex -= 1;
        }

        private void Page1SurcodeBrowseButton_Click(object sender, EventArgs e) {
            using (OpenFileDialog Page1OpenFileDialog = new OpenFileDialog()) {
                Page1OpenFileDialog.AutoUpgradeEnabled = true;
                Page1OpenFileDialog.CheckFileExists = true;
                Page1OpenFileDialog.Multiselect = false;
                Page1OpenFileDialog.Filter = "surcodemlp.exe|surcodemlp.exe";

                if (Page1OpenFileDialog.ShowDialog() == DialogResult.OK) {
                    this.Page1SurcodeTextBox.Text = Page1OpenFileDialog.FileName;
                    SaveSettings();
                }
            }

        }

        private void Page1eac3toBrowseButton_Click(object sender, EventArgs e) {
            using (OpenFileDialog Page1OpenFileDialog = new OpenFileDialog()) {
                Page1OpenFileDialog.AutoUpgradeEnabled = true;
                Page1OpenFileDialog.CheckFileExists = true;
                Page1OpenFileDialog.Multiselect = false;
                Page1OpenFileDialog.Filter = "eac3to.exe|eac3to.exe";

                if (Page1OpenFileDialog.ShowDialog() == DialogResult.OK) {
                    this.Page1eac3toTextbox.Text = Page1OpenFileDialog.FileName;
                    SaveSettings();
                }
            }

        }

        private void Page2AddFilesButton_Click(object sender, EventArgs e) {
            using (OpenFileDialog Page2OpenFileDialog = new OpenFileDialog()) {
                Page2OpenFileDialog.AutoUpgradeEnabled = true;
                Page2OpenFileDialog.CheckFileExists = true;
                Page2OpenFileDialog.Multiselect = true;

                if (Page2OpenFileDialog.ShowDialog() == DialogResult.OK) {
                    foreach (string FileFullName in Page2OpenFileDialog.FileNames) {
                        AddFile(FileFullName);
                    }
                }
            }

        }

        private void AddFile(string FileFullName) {

            MediaInfo MediaFile = new MediaInfo();
            string[] FileInfo = new string[6];
            FileInfo[0] = System.IO.Path.GetFileNameWithoutExtension(FileFullName);
            FileInfo[5] = FileFullName;
            string OriginalMessage = string.Empty;
            try {
                //Get the properties
                MediaFile.Open(FileFullName);
                FileInfo[2] = MediaFile.Get(StreamKind.Audio, 0, "Duration");
                FileInfo[3] = MediaFile.Get(StreamKind.Audio, 0, "SamplingRate");
                FileInfo[4] = MediaFile.Get(StreamKind.Audio, 0, "BitDepth");
                FileInfo[1] = MediaFile.Get(StreamKind.Audio, 0, "Codec");

            }
            catch (Exception ex) {
                OriginalMessage = ex.Message;
                FileInfo[2] = FileInfo[1] = string.Empty;
                FileInfo[3] = FileInfo[4] = "0";
            }
            MediaFile.Close();

            bool Canceled = false;

            CheckAndChangeProperties(ref FileInfo, OriginalMessage, false, ref Canceled);

            if (Canceled) return;
            ListViewItem FileItem = new ListViewItem(FileInfo);
            this.Page2ListView.Items.Add(FileItem);
            CheckListView();

        }

        private void CheckAndChangeProperties(ref string[] FileInfo, string OriginalMessage, bool Manually, ref bool Canceled) {

            Debug.Assert(FileInfo.Length == 6);
            bool FileInfoCorrect = !Manually;
            bool NameExists = false, SamplingRateIncorrect = false, BitDepthIncorrect = false, NameIllegal = false, DurationIncorrect = false;
            do {
                if (!FileInfoCorrect) {
                    using (PropertyForm Form = new PropertyForm()) {

                        string Message = OriginalMessage;
                        if (NameIllegal) Message += MainFormCodeStrings.CheckAndChangePropertiesNameIllegalString + Environment.NewLine;
                        if (NameExists) Message += MainFormCodeStrings.CheckAndChangePropertiesNameExistsString + Environment.NewLine;
                        if (SamplingRateIncorrect) Message += MainFormCodeStrings.CheckAndChangePropertiesSamplingRateIncorrectString + Environment.NewLine;
                        if (BitDepthIncorrect) Message += MainFormCodeStrings.CheckAndChangePropertiesBitDepthIncorrectString + Environment.NewLine;
                        if (DurationIncorrect) Message += MainFormCodeStrings.CheckAndChangePropertiesDurationIncorrectString + Environment.NewLine;

                        Canceled = false;
                        Form.ShowDialog(ref FileInfo, Message, ref Canceled);

                        if (Canceled) {
                            return;
                        }

                    }

                }

                CheckInfo(FileInfo, ref NameIllegal, ref NameExists, ref SamplingRateIncorrect, ref BitDepthIncorrect, ref DurationIncorrect);
                FileInfoCorrect = !(NameIllegal || NameExists || SamplingRateIncorrect || BitDepthIncorrect || DurationIncorrect);

            } while (!FileInfoCorrect);
        }

        private void CheckInfo(string[] FileInfo, ref bool NameIllegal, ref bool NameExists, ref bool SamplingRateIncorrect, ref bool BitDepthIncorrect, ref bool DurationIncorrect) {
            Debug.Assert(FileInfo.Length == 6);

            NameExists = false;

            if (String.IsNullOrEmpty(FileInfo[0]) || FileInfo[0].IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) {
                NameIllegal = true;
            }

            else {
                NameIllegal = false;
                foreach (ListViewItem Item in this.Page2ListView.Items) {
                    if (Item.SubItems[0].Text == FileInfo[0]) {
                        NameExists = true;
                        break;
                    }
                }
            }

            if (Microsoft.VisualBasic.Information.IsNumeric(FileInfo[3])) {
                SamplingRateIncorrect = (Convert.ToInt32(FileInfo[3], CultureInfo.CurrentCulture) <= 0);
            }
            else {
                SamplingRateIncorrect = true;
            }

            if (Microsoft.VisualBasic.Information.IsNumeric(FileInfo[4])) {
                BitDepthIncorrect = (Convert.ToInt32(FileInfo[4], CultureInfo.CurrentCulture) <= 0);
            }
            else {
                BitDepthIncorrect = true;
            }

            if (Microsoft.VisualBasic.Information.IsNumeric(FileInfo[2])) {
                DurationIncorrect = Convert.ToInt32(FileInfo[2], CultureInfo.CurrentCulture) <= 0;
            }
            else {
                DurationIncorrect = true;
            }

        }
        //private IntPtr RunSurcode(bool Hidden, bool CloseAtOnce) {
        private IntPtr RunSurcode(bool CloseAtOnce) {
            //  Int32 WaitTimes = Decimal.ToInt32(Page4WaitTimes.Value)

            Int32 SmallWaitInterval = Decimal.ToInt32(this.Page4B.Value);
            Int32 SmallWaitTimes = Decimal.ToInt32(this.Page4A.Value / this.Page4B.Value) + 1;
            string SurcodeFileFullName = this.Page1SurcodeTextBox.Text;
            string SurcodeFilePath = System.IO.Path.GetDirectoryName(SurcodeFileFullName);
            Debug.WriteLine(SurcodeFilePath);

            IntPtr hWnd;
            //1. looking for other window
            do {
                hWnd = NativeMethods.FindWindowExW(IntPtr.Zero, IntPtr.Zero, null, "MLP Encoder");
                if (hWnd != IntPtr.Zero) NativeMethods.SendMessageW(hWnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, string.Empty);
            } while (hWnd != IntPtr.Zero);

            if (!CloseAtOnce) {
                do {
                    hWnd = NativeMethods.FindWindowExW(IntPtr.Zero, IntPtr.Zero, null, "MLP Encoder Log File");
                    if (hWnd != IntPtr.Zero) NativeMethods.SendMessageW(hWnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, string.Empty);
                } while (hWnd != IntPtr.Zero);
            }

            //2. start surcode
            using (Process SurcodeProcess = new Process()) {
                SurcodeProcess.StartInfo.UseShellExecute = true;
                SurcodeProcess.StartInfo.FileName = SurcodeFileFullName;
                SurcodeProcess.StartInfo.Arguments = string.Empty;
                SurcodeProcess.StartInfo.WorkingDirectory = SurcodeFilePath;
                //if (Hidden) {
                //    SurcodeProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                //}
                //else {
                //    SurcodeProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                //}

                try {
                    SurcodeProcess.Start();

                    for (int k = 0; k < SmallWaitTimes; ++k) {
                        Thread.Sleep(SmallWaitInterval);
                        hWnd = NativeMethods.FindWindowExW(IntPtr.Zero, IntPtr.Zero, null, "MLP Encoder");
                        if (hWnd != IntPtr.Zero) {
                            Debug.WriteLine("Start time = " + SmallWaitInterval * (k + 1) + " ms");
                            break;
                        }
                    }

                    if (hWnd == IntPtr.Zero) {
                        throw new Exception(MainFormCodeStrings.RunSurcodeTimeoutString);
                    }
                    else {
                        NativeMethods.SendMessageW(hWnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, "MLP Encoder - In Control");

                        if (CloseAtOnce) {
                            NativeMethods.SendMessageW(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            return IntPtr.Zero;
                        }
                        else {
                            //Success
                            return hWnd;
                        }
                    }

                }
                catch (Exception ex) {
                    string Message = MainFormCodeStrings.RunSurcodeFailedString + Environment.NewLine + ex.Message;
                    //hWnd = IntPtr.Zero;
                    throw new Exception(Message);
                }
            }


            //hWnd = NativeMethods.ShellExecuteW(IntPtr.Zero, "open", SurcodeFileFullName, null, SurcodeFilePath, WindowState);
            //if (hWnd.ToInt32() <= 32) // x86 build only!
            //{
            //    string Message = "Can not start Surcode MLP Encoder. Error code " + hWnd.ToString() + "." + Environment.NewLine + ErrorCodeToString(hWnd);
            //    hWnd = IntPtr.Zero;
            //    throw new Exception(Message);
            //}
            //else {
            //    for (int k = 0; k < SmallWaitTimes; ++k) {
            //        System.Threading.Thread.Sleep(SmallWaitInterval);
            //        hWnd = NativeMethods.FindWindowExW(IntPtr.Zero, IntPtr.Zero, null, "MLP Encoder");
            //        if (hWnd != IntPtr.Zero) {
            //            Debug.WriteLine("Start time = " + SmallWaitInterval * (k + 1) + " ms");
            //            break;
            //        }
            //    }

            //    if (hWnd == IntPtr.Zero) {
            //        throw new Exception("Can not start Surcode MLP Encoder. Time's out.");
            //    }
            //    else {
            //        NativeMethods.SendMessageW(hWnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, "MLP Encoder - In Control");

            //        if (CloseAtOnce) {
            //            NativeMethods.SendMessageW(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            //            return IntPtr.Zero;
            //        }
            //        else {
            //            //Success
            //            return hWnd;
            //        }
            //    }
            //}

        }

        //private static string ErrorCodeToString(IntPtr hWnd) {
        //    switch (hWnd.ToInt32()) {
        //        case 0:
        //            return "The operating system is out of memory or resources.";
        //        case 2:
        //            return "The specified file was not found.";
        //        case 3:
        //            return "The specified path was not found.";
        //        case 11:
        //            return "The .exe file is invalid (non-Win32 .exe or error in .exe image).";
        //        case 5:
        //            return "The operating system denied access to the specified file.";
        //        case 27:
        //            return "The file name association is incomplete or invalid.";
        //        case 30:
        //            return "The DDE transaction could not be completed because other DDE transactions were being processed.";
        //        case 29:
        //            return "The DDE transaction failed.";
        //        case 28:
        //            return "The DDE transaction could not be completed because the request timed out.";
        //        case 32:
        //            return "The specified DLL was not found.";
        //        case 31:
        //            return "There is no application associated with the given file name extension. This error will also be returned if you attempt to print a file that is not printable.";
        //        case 8:
        //            return "There was not enough memory to complete the operation.";
        //        case 26:
        //            return "A sharing violation occurred.";
        //        default:
        //            return string.Empty;
        //    }
        //}
        private void OpenSurcodeOptions(IntPtr hWnd) {

            AutomationElement SurcodeElement = AutomationElement.FromHandle(hWnd);

            AutomationElement Menu2Element = SurcodeElement.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem), new PropertyCondition(AutomationElement.AutomationIdProperty, "Item 2")));
            if (Menu2Element == null || Menu2Element.GetCurrentPropertyValue(AutomationElement.NameProperty).ToString() != "Options") {
                throw new Exception(MainFormCodeStrings.OpenSurcodeOptionsCannotFindOptionsMenuString);
            }

            try {
                ExpandCollapsePattern Menu2Pattern = (ExpandCollapsePattern)Menu2Element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
                Menu2Pattern.Expand();
            }
            catch (Exception ex) {
                throw new Exception(MainFormCodeStrings.OpenSurcodeOptionsCannotExpandOptionsMenuString + Environment.NewLine + ex.Message);
            }

            Thread.Sleep(Convert.ToInt32(this.Page4B.Value, CultureInfo.InvariantCulture) * 10);

            AutomationElement Menu2EncoderOptionElement = Menu2Element.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem), new PropertyCondition(AutomationElement.AutomationIdProperty, "Item 32772")));
            if (Menu2EncoderOptionElement == null || Menu2EncoderOptionElement.GetCurrentPropertyValue(AutomationElement.NameProperty).ToString() != "Encoder Options...") {
                throw new Exception(MainFormCodeStrings.OpenSurcodeOptionsCannotFindEncoderOptionsMenuString);
            }

            try {
                InvokePattern Menu2EncoderOptionPattern = (InvokePattern)Menu2EncoderOptionElement.GetCurrentPattern(InvokePattern.Pattern);
                Menu2EncoderOptionPattern.Invoke();
            }
            catch (Exception ex) {
                throw new Exception(MainFormCodeStrings.OpenSurcodeOptionsCannotClickEncoderOptionsMenuString + Environment.NewLine + ex.Message);
            }

            NativeMethods.SendMessageW(hWnd, NativeMethods.WM_SETTEXT, IntPtr.Zero, "MLP Encoder");
        }

        private void Page4SurcodeOptionButton_Click(object sender, EventArgs e) {
            this.Page4SurcodeOptionButton.Enabled = false;
            this.Page4BackgroundWorker.RunWorkerAsync();
        }

        private void Page2RemoveButton_Click(object sender, EventArgs e) {
            foreach (ListViewItem Item in this.Page2ListView.SelectedItems) {
                this.Page2ListView.Items.Remove(Item);
            }
            CheckListView();
        }

        private void Page2ListView_SelectedIndexChanged(object sender, EventArgs e) {
            CheckListView();
        }

        private void Page2ClearAllButton_Click(object sender, EventArgs e) {
            this.Page2ListView.Items.Clear();
            CheckListView();
        }

        private void CheckListView() {
            /* Page2NextButton.Enabled =*/
            this.Page2ClearAllButton.Enabled = this.Page2ListView.Items.Count > 0;
            if (this.Page2ListView.SelectedItems.Count == 1) {
                this.Page2RemoveButton.Enabled = this.Page2RenameButton.Enabled = this.Page2PropertyButton.Enabled = true;
            }
            else if (this.Page2ListView.SelectedItems.Count == 0) {
                this.Page2RemoveButton.Enabled = this.Page2RenameButton.Enabled = this.Page2PropertyButton.Enabled = false;
            }
            else {
                this.Page2RemoveButton.Enabled = true;
                this.Page2RenameButton.Enabled = this.Page2PropertyButton.Enabled = false;
            }

        }

        private void Page2ListView_DragDrop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] FileFullnames = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string FileFullname in FileFullnames) {
                    AddFile(FileFullname);
                }

            }
        }

        private void Page2ListView_DragEnter(object sender, DragEventArgs e) {

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void Page2PropertyButton_Click(object sender, EventArgs e) {
            Debug.Assert(this.Page2ListView.SelectedItems.Count == 1);
            bool Canceled = false;
            string Message = MainFormCodeStrings.Page2PropertyButtonClickMessageStringLeft + (sender as Button).Text.Replace("&", "") + MainFormCodeStrings.Page2PropertyButtonClickMessageStringRight + Environment.NewLine;
            string[] FileInfo = new string[6];
            for (int i = 0; i < 6; ++i) {
                FileInfo[i] = this.Page2ListView.SelectedItems[0].SubItems[i].Text;
                Debug.WriteLine("FileInfo[" + i.ToString(CultureInfo.InvariantCulture) + "]=" + FileInfo[i]);
            }

            //Aviod the same filename
            string OriginalFileName = this.Page2ListView.SelectedItems[0].SubItems[0].Text;
            this.Page2ListView.SelectedItems[0].SubItems[0].Text = '*' + this.Page2ListView.SelectedItems[0].SubItems[0].Text;

            CheckAndChangeProperties(ref FileInfo, Message, true, ref Canceled);

            if (!Canceled) {
                for (int i = 0; i < 6; ++i) {
                    this.Page2ListView.SelectedItems[0].SubItems[i].Text = FileInfo[i];
                }
            }
            else {
                this.Page2ListView.SelectedItems[0].SubItems[0].Text = OriginalFileName;
            }

        }

        private void Page3DownDplRadioButton_CheckedChanged(object sender, EventArgs e) {

            this.Page3DplMixlfeCheckBox.Enabled = this.Page3DplPhaseShiftCheckBox.Enabled = this.Page3DownDplRadioButton.Checked;

        }

        private void Page4CheckBox_CheckedChanged(object sender, EventArgs e) {
            this.Page4GroupBox.Enabled = this.Page4CheckBox.Checked;
        }

        //private void MainTabControl_SelectedIndexChanged(object sender, EventArgs e) {
        //    {
        //        // SaveSettings();
        //    }

        //}

        private void Page4BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            string Message = string.Empty;
            e.Result = string.Empty;
            IntPtr hWnd = IntPtr.Zero;
            Int32 RetryTimes = decimal.ToInt32(this.Page4C.Value);
            for (int i = 1; i <= RetryTimes; ++i) {
                try {
                    hWnd = RunSurcode(false);
                    OpenSurcodeOptions(hWnd);
                    break;
                }
                catch (Exception ex) {
                    if (hWnd.ToInt32() > 32) {
                        NativeMethods.SendMessageW(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                    Debug.WriteLine(ex.Message);
                    Message += i.ToString(CultureInfo.CurrentCulture) + ": " + ex.Message + Environment.NewLine;
                    if (i == RetryTimes) {
                        e.Result = Message;
                    }
                    else {
                        Thread.Sleep(decimal.ToInt32(this.Page4B.Value));
                    }
                }
            }
        }

        private void Page5SaveButton_Click(object sender, EventArgs e) {
            using (FolderBrowserDialog Page5FolderDialog = new FolderBrowserDialog()) {
                Page5FolderDialog.ShowNewFolderButton = true;
                if (Page5FolderDialog.ShowDialog() == DialogResult.OK) {
                    this.Page5SaveTextbox.Text = Page5FolderDialog.SelectedPath;
                    SaveSettings();
                }
            }

        }

        private void Page5TempButton_Click(object sender, EventArgs e) {
            using (FolderBrowserDialog Page5FolderDialog = new FolderBrowserDialog()) {
                Page5FolderDialog.ShowNewFolderButton = true;
                if (Page5FolderDialog.ShowDialog() == DialogResult.OK) {
                    this.Page5TempTextbox.Text = Page5FolderDialog.SelectedPath;
                    SaveSettings();
                }
            }
        }

        private void Page4BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (!String.IsNullOrEmpty(e.Result.ToString())) {
                MessageBox.Show(e.Result.ToString(), MainFormCodeStrings.Page4BackgroundWorkerRunWorkerCompletedErrorString, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            this.Page4SurcodeOptionButton.Enabled = true;
        }

        private void Page5StartButton_Click(object sender, EventArgs e) {
            //1. Check
            if (!System.IO.File.Exists(this.Page1SurcodeTextBox.Text)) {
                MessageBox.Show(MainFormCodeStrings.Page5StartButtonClickCannotFindSurcodeString, MainFormCodeStrings.Page5StartButtonClickMissingFileString, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.MainTabControl.SelectedIndex = 0;
                return;
            }
            if (!System.IO.File.Exists(this.Page1eac3toTextbox.Text)) {
                MessageBox.Show(MainFormCodeStrings.Page5StartButtonClickCannotFindEac3toString, MainFormCodeStrings.Page5StartButtonClickMissingFileString, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.MainTabControl.SelectedIndex = 0;
                return;
            }
            if (!System.IO.Directory.Exists(this.Page5TempTextbox.Text)) {
                MessageBox.Show(MainFormCodeStrings.Page5StartButtonClickCannotFindTempFolderString, MainFormCodeStrings.Page5StartButtonClickMissingDirectoryString, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.MainTabControl.SelectedIndex = 4;
                return;
            }
            if (!System.IO.Directory.Exists(this.Page5SaveTextbox.Text)) {
                MessageBox.Show(MainFormCodeStrings.Page5StartButtonClickCannotFindOutputFolderString, MainFormCodeStrings.Page5StartButtonClickMissingDirectoryString, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.MainTabControl.SelectedIndex = 4;
                return;
            }

            //Surcode 通过 ANSI 编码的 .ssf 文件读取文件路径，文件夹名含韩文等无法编码的字符时同样会失败
            if (ContainsHangul(this.Page5TempTextbox.Text) || !CanBeAnsiEncoded(this.Page5TempTextbox.Text) ||
                ContainsHangul(this.Page5SaveTextbox.Text) || !CanBeAnsiEncoded(this.Page5SaveTextbox.Text)) {
                if (MessageBox.Show(MainFormCodeStrings.SurcodeSafeFileNameFolderWarningString, MainFormCodeStrings.SurcodeSafeFileNameFolderWarningCaptionString, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
                    return;
                }
            }

            //2.
            this.eac3Processing = this.SurcodeProcessing = this.Processing = true;
            this.MainTabControl.SelectedIndex = 5;
            this.Page6eac3toProgressBar.Value = this.Page6eac3toProgressBar.Minimum;
            this.Page6SurcodeProgressBar.Value = this.Page6SurcodeProgressBar.Minimum;
            this.Rebit20BitWarned = false;
            this.RenameFailureCount = 0;
            this.eac3toOK = new bool[this.Page2ListView.Items.Count];
            this.eac3toFailed = new bool[this.Page2ListView.Items.Count];
            this.SurcodeFailed = new bool[this.Page2ListView.Items.Count];
            this.SurcodeSucceeded = new bool[this.Page2ListView.Items.Count];
            Array.Clear(this.eac3toOK, 0, this.eac3toOK.Length);
            Array.Clear(this.eac3toFailed, 0, this.eac3toFailed.Length);
            Array.Clear(this.SurcodeFailed, 0, this.SurcodeFailed.Length);
            Array.Clear(this.SurcodeSucceeded, 0, this.SurcodeSucceeded.Length);
            this.Page1Panel.Enabled = this.Page2Panel.Enabled = this.Page3Panel.Enabled = this.Page4Panel.Enabled = this.Page5Panel.Enabled = false;
            this.Files = new string[this.Page2ListView.Items.Count, 6];
            for (int i = 0; i < this.Page2ListView.Items.Count; ++i)
                for (int j = 0; j < 6; ++j)
                    this.Files[i, j] = this.Page2ListView.Items[i].SubItems[j].Text;

            //批处理模式始终使用很短的纯 ASCII 编码名。Surcode 的 .ssf 格式用单字节保存
            //每个路径字段，最多只能表示 255 个 ANSI 字节；即使 Windows/.NET 支持长路径，
            //原始长文件名仍可能令 .ssf 无法表示。GUI 模式则只替换不兼容的名称。
            //MLP 生成之后再统一改回原文件名，见 RenameMlpFiles()。
            this.EncodeNames = new string[this.Page2ListView.Items.Count];
            this.NameReplaced = new bool[this.Page2ListView.Items.Count];
            for (int i = 0; i < this.Page2ListView.Items.Count; ++i) {
                if (this.BatchOptions != null || NeedSurcodeSafeName(this.Files[i, 0])) {
                    this.EncodeNames[i] = this.MakeSurcodeSafeName(i);
                    this.NameReplaced[i] = true;
                    Debug.WriteLine("Surcode safe name: " + this.Files[i, 0] + " -> " + this.EncodeNames[i]);
                }
                else {
                    this.EncodeNames[i] = this.Files[i, 0];
                    this.NameReplaced[i] = false;
                }
            }

            this.Page6CancelButton.Enabled = true;
            this.Page6eac3toListView.Items.Clear();
            this.Page6SurcodeListView.Items.Clear();

            //3. Start threads
            this.Page6eac3toBackgroundWorker.RunWorkerAsync();
            this.Page6SurcodeBackgroundWorker.RunWorkerAsync();
        }

        private void MainTabControl_Selecting(object sender, TabControlCancelEventArgs e) {
            if (this.Processing) {
                if (e.TabPageIndex < 5) {
                    e.Cancel = true;
                }
            }

            else {
                if (e.TabPageIndex == 5) {
                    // e.Cancel = true;
                }
            }
        }

        private Int32 GetChosenSamplingRate() {
            if (this.Page3_44100RadioButton.Checked) return 44100;
            else if (this.Page3_48000RadioButton.Checked) return 48000;
            else if (this.Page3_88200RadioButton.Checked) return 88200;
            else if (this.Page3_96000RadioButton.Checked) return 96000;
            else if (this.Page3_176400RadioButton.Checked) return 176400;
            else if (this.Page3_192000RadioButton.Checked) return 192000;
            else {
                Debug.Assert(false);
                throw new Exception("None of the sampling rates are chosen.");
            }

        }
        private Int32 GetChosenBitDepth() {
            if (this.Page3_16RadioButton.Checked) return 16;
            else if (this.Page3_20RadioButton.Checked) return 20;
            else if (this.Page3_24RadioButton.Checked) return 24;
            else {
                Debug.Assert(false);
                throw new Exception("None of the bit depth are chosen.");
            }

        }

        /// <summary>
        /// DVD-Audio 允许的位深。
        /// </summary>
        private static bool IsAllowedBitDepth(int BitDepth) {
            return BitDepth == 16 || BitDepth == 20 || BitDepth == 24;
        }

        /// <summary>
        /// DVD-Audio 允许的采样率。
        /// </summary>
        private static bool IsAllowedSamplingRate(string SamplingRate) {
            return SamplingRate == "44100" || SamplingRate == "48000" || SamplingRate == "88200"
                || SamplingRate == "96000" || SamplingRate == "176400" || SamplingRate == "192000";
        }

        /// <summary>
        /// 判断在当前采样率设置下，eac3to 会不会真的对某个源采样率做重采样。
        /// 这一点很重要：eac3to 重采样时内部走 64 bit 浮点管线，最终固定输出 24 bit。
        /// 注意“把所有的...”模式下，若源采样率恰好等于目标采样率，eac3to 会检测到并跳过重采样。
        /// </summary>
        private bool WillEac3toResample(string SourceSamplingRate) {
            if (this.Page3OnlyResampleRadioButton.Checked) {
                return !MainForm.IsAllowedSamplingRate(SourceSamplingRate);
            }
            if (this.Page3AlwaysResampleRadioButton.Checked) {
                string Chosen = GetChosenSamplingRate().ToString(CultureInfo.InvariantCulture);
                return !String.Equals(SourceSamplingRate, Chosen, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>
        /// 读取当前的位深设置。返回 false 表示不需要做任何位深处理（“不转换任何文件”）。
        /// </summary>
        /// <param name="Chosen">目标位深</param>
        /// <param name="OnlyNotAllowed">true 表示只处理位深不合法的文件（16/20/24 之外）</param>
        private bool GetRebitSetting(out int Chosen, out bool OnlyNotAllowed) {
            //“强制 16 bits” 是个复选框，优先级最高，与原来 if/else 链的顺序保持一致
            if (this.Always16bitsRadioButton.Checked) {
                Chosen = 16;
                OnlyNotAllowed = false;
                return true;
            }
            if (this.Page3AlwaysRebitRadioButton.Checked) {
                Chosen = GetChosenBitDepth();
                OnlyNotAllowed = false;
                return true;
            }
            if (this.Page3OnlyRebitRadioButton.Checked) {
                Chosen = GetChosenBitDepth();
                OnlyNotAllowed = true;
                return true;
            }
            Chosen = 0;
            OnlyNotAllowed = false;
            return false;
        }

        /// <summary>
        /// 位深处理方案。
        /// </summary>
        private class RebitDecision {
            /// <summary>要追加到 eac3to 命令行的参数。</summary>
            public string Arguments = string.Empty;
            /// <summary>是否需要先用 ffmpeg 做无损位深扩展。</summary>
            public bool NeedUpconvert;
            /// <summary>ffmpeg 的目标位深。没有 20 bit 的 WAV 容器，所以只可能是 16 或 24。</summary>
            public int UpconvertBits;
            /// <summary>要在界面上显示给用户的提示，不需要时为空字符串。</summary>
            public string Message = string.Empty;
        }

        /// <summary>
        /// 决定某个文件的位深处理方案。
        /// eac3to 的 -downNN 只能降低位深、无法提升；需要提升时由本程序自己重写 WAV
        /// （采样左移补零，不损失任何音频数据），见 ConvertWavBitDepth。
        /// </summary>
        /// <param name="Index">文件序号</param>
        /// <param name="Chosen">目标位深</param>
        /// <param name="OnlyNotAllowed">true 表示只处理位深不合法的文件</param>
        /// <param name="ResamplingOccurs">eac3to 是否会对该文件做重采样</param>
        private RebitDecision DecideRebit(int Index, int Chosen, bool OnlyNotAllowed, bool ResamplingOccurs) {
            RebitDecision Decision = new RebitDecision();

            int Source;
            if (!Int32.TryParse(this.Files[Index, 4], NumberStyles.Integer, CultureInfo.InvariantCulture, out Source) || Source <= 0) {
                //位深未知，交给 eac3to 自己判断；-downNN 不会提升位深，所以传进去总是安全的
                Decision.Arguments = " -down" + Chosen.ToString(CultureInfo.InvariantCulture);
                return Decision;
            }

            if (OnlyNotAllowed && MainForm.IsAllowedBitDepth(Source)) {
                //位深本身合法（16/20/24），不需要处理
                return Decision;
            }

            //eac3to 处理完之后实际会输出多少位：
            //重采样时它内部是 64 bit 浮点管线，最终固定输出 24 bit；否则保持源位深。
            int Eac3toOutputBits = ResamplingOccurs ? 24 : Source;

            if (Chosen < Eac3toOutputBits) {
                Decision.Arguments = " -down" + Chosen.ToString(CultureInfo.InvariantCulture);
                if (Chosen == 20 && !this.Rebit20BitWarned) {
                    this.Rebit20BitWarned = true;
                    Decision.Message = MainFormCodeStrings.RebitTwentyBitContainerString;
                }
                return Decision;
            }

            if (Chosen == Eac3toOutputBits) {
                //已经符合要求。eac3to 收到同值参数也会跳过，所以干脆不传
                return Decision;
            }

            //Chosen > Eac3toOutputBits：需要提升位深，eac3to 做不到，由本程序自己重写 WAV
            Decision.NeedUpconvert = true;
            //没有 20 bit 的 WAV 容器，选 20 bit 时只能装进 24 bit
            Decision.UpconvertBits = (Chosen == 16) ? 16 : 24;

            if (Chosen == 20) {
                if (!this.Rebit20BitWarned) {
                    this.Rebit20BitWarned = true;
                    Decision.Message = MainFormCodeStrings.RebitTwentyBitContainerString;
                }
            }
            else {
                Decision.Message = string.Format(CultureInfo.CurrentCulture, MainFormCodeStrings.RebitUpconvertString, Source, Decision.UpconvertBits);
            }
            return Decision;
        }

        /// <summary>
        /// 判断字符串中是否含有谚文（韩文）字符。
        /// Surcode MLP Encoder 不能正确处理含韩文的文件名，会报错或找不到文件。
        /// </summary>
        private static bool ContainsHangul(string Value) {
            if (string.IsNullOrEmpty(Value)) return false;
            foreach (char Character in Value) {
                if ((Character >= '\u1100' && Character <= '\u11FF') || //谚文字母
                    (Character >= '\u3130' && Character <= '\u318F') || //谚文兼容字母
                    (Character >= '\uA960' && Character <= '\uA97F') || //谚文字母扩展-A
                    (Character >= '\uAC00' && Character <= '\uD7A3') || //谚文音节
                    (Character >= '\uD7B0' && Character <= '\uD7FF') || //谚文字母扩展-B
                    (Character >= '\uFFA0' && Character <= '\uFFDC')) { //半角谚文
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断字符串能否用当前系统 ANSI 代码页表示。
        /// .ssf 文件是按系统 ANSI 代码页写出的（见 LegacyTextEncoding），
        /// 无法表示的字符会变成 '?'，Surcode 因而找不到文件。
        /// </summary>
        private static bool CanBeAnsiEncoded(string Value) {
            return LegacyTextEncoding.CanBeAnsiEncoded(Value);
        }

        /// <summary>
        /// 判断文件名是否需要替换成临时的纯 ASCII 名才能交给 Surcode MLP Encoder 处理。
        /// </summary>
        private static bool NeedSurcodeSafeName(string FileName) {
            return ContainsHangul(FileName) || !CanBeAnsiEncoded(FileName);
        }

        /// <summary>
        /// 生成一个纯 ASCII 的临时文件名（不含扩展名），并保证不与列表中其它文件名冲突。
        /// </summary>
        private string MakeSurcodeSafeName(int Index) {
            for (int Suffix = 0; ; ++Suffix) {
                string Name = "__surcode_" + (Index + 1).ToString("D4", CultureInfo.InvariantCulture);
                if (Suffix > 0) Name += "_" + Suffix.ToString(CultureInfo.InvariantCulture);

                bool Exists = false;
                if (this.Files != null) {
                    for (int i = 0; i < this.Files.GetLength(0); ++i) {
                        if (String.Equals(this.Files[i, 0], Name, StringComparison.OrdinalIgnoreCase)) {
                            Exists = true;
                            break;
                        }
                    }
                }
                if (!Exists) return Name;
            }
        }

        /// <summary>
        /// 删除文件，失败时忽略（例如文件正被占用）。
        /// </summary>
        private static bool TryDeleteFile(string FileFullName) {
            try {
                if (System.IO.File.Exists(FileFullName)) {
                    System.IO.File.Delete(FileFullName);
                    return true;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine("Can not delete " + FileFullName + ": " + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// 反复尝试删除文件，直到成功、文件不存在或超时。不会抛出异常。
        /// 上一次尝试的残留文件、或用户正在播放的同名文件都可能被占用，
        /// 因此删除失败不应该直接让整个转换失败。
        /// </summary>
        private bool TryDeleteFileWithRetry(string FileFullName, int TimeoutMilliseconds) {
            DateTime Deadline = DateTime.UtcNow.AddMilliseconds(TimeoutMilliseconds);
            do {
                if (!System.IO.File.Exists(FileFullName)) return true;
                MainForm.TryDeleteFile(FileFullName);
                if (!System.IO.File.Exists(FileFullName)) return true;
                if (this.Page6SurcodeBackgroundWorker.CancellationPending) return false;
                Thread.Sleep(MainForm.FileRetryIntervalMilliseconds);
            } while (DateTime.UtcNow < Deadline);
            Debug.WriteLine("Can not delete (still in use): " + FileFullName);
            return false;
        }

        /// <summary>
        /// 反复尝试移动（改名）文件，直到成功或超时，失败时抛出异常。
        /// 目标位置可能残留同名文件，Windows 索引、杀毒软件也可能短暂占用，
        /// 所以不能只试一次，否则会报“另一个进程正在使用此文件”。
        /// 注意：这里不理会取消请求——改名是最后一步收尾，中断它只会留下临时文件名。
        /// </summary>
        private void MoveFileWithRetry(string SourceFullName, string DestFullName, int TimeoutMilliseconds, int ProgressPercentage) {
            DateTime Deadline = DateTime.UtcNow.AddMilliseconds(TimeoutMilliseconds);
            Exception LastError = null;
            bool Reported = false;
            do {
                try {
                    System.IO.File.Move(SourceFullName, DestFullName);
                    return;
                }
                catch (System.IO.IOException ex) {
                    LastError = ex;
                }
                catch (UnauthorizedAccessException ex) {
                    LastError = ex;
                }

                //目标位置可能残留了同名文件；如果能删掉，下一次尝试就是纯粹的改名
                MainForm.TryDeleteFile(DestFullName);

                if (!Reported) {
                    Reported = true;
                    this.Page6SurcodeBackgroundWorker.ReportProgress(ProgressPercentage, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, MainFormCodeStrings.WaitingForFileString, string.Empty, false));
                }

                if (DateTime.UtcNow >= Deadline) break;
                Thread.Sleep(MainForm.FileRetryIntervalMilliseconds);
            } while (true);

            throw new System.IO.IOException(LastError == null ? string.Empty : LastError.Message, LastError);
        }

        /// <summary>
        /// 把所有临时 ASCII 名的 MLP 文件改回原文件名。
        /// 统一放在所有文件都编码完成之后执行：那时 Surcode 早已退出、文件句柄已释放，
        /// 比“编完一个就立刻改名”可靠得多（后者常常撞上 Surcode 尚未释放句柄）。
        /// 每个文件单独容错，某一个改名失败不影响其它文件；文件内容本身是正确的，
        /// 所以改名失败不计入“转换失败”。
        /// </summary>
        private void RenameMlpFiles() {
            if (this.NameReplaced == null || this.SurcodeSucceeded == null || this.EncodeNames == null || this.Files == null) return;

            int Renamed = 0, Failed = 0;
            for (int i = 0; i < this.NameReplaced.Length; ++i) {
                if (!this.NameReplaced[i] || !this.SurcodeSucceeded[i]) continue;

                string MlpFileFullName = this.Page5SaveTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + ".mlp";
                string OriginalMlpFileFullName = this.Page5SaveTextbox.Text.TrimEnd('\\') + '\\' + this.Files[i, 0] + ".mlp";

                if (!System.IO.File.Exists(MlpFileFullName)) continue;

                this.TryDeleteFileWithRetry(OriginalMlpFileFullName, MainForm.ShortFileWaitMilliseconds);
                try {
                    this.MoveFileWithRetry(MlpFileFullName, OriginalMlpFileFullName, MainForm.RenameWaitMilliseconds, i * 100 / this.NameReplaced.Length);
                    ++Renamed;
                    Debug.WriteLine("Renamed MLP: " + MlpFileFullName + " -> " + OriginalMlpFileFullName);
                }
                catch (Exception ex) {
                    ++Failed;
                    //不设置 SurcodeFailed[i]：音频已经正确转换完成，只是文件名不对，
                    //不应该让用户重跑一遍编码
                    this.Page6SurcodeBackgroundWorker.ReportProgress(i * 100 / this.NameReplaced.Length,
                        new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add,
                            string.Format(CultureInfo.CurrentCulture, MainFormCodeStrings.RenameMlpFailedString, MlpFileFullName, OriginalMlpFileFullName)
                            + Environment.NewLine + ex.Message,
                            this.Files[i, 0], true));
                }
            }
            Debug.WriteLine("RenameMlpFiles: renamed=" + Renamed + " failed=" + Failed);
            this.RenameFailureCount = Failed;
        }

        /// <summary>
        /// 读取 WAV 的位深。优先读 WAVE_FORMAT_EXTENSIBLE 里的“有效位”字段，
        /// 因为容器位数（24）和真实精度（20）可能不一致。
        /// 读不出来时返回 0。
        /// </summary>
        private static int ReadWavBitDepth(string FileFullName) {
            try {
                using (System.IO.FileStream Stream = new System.IO.FileStream(FileFullName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)) {
                    byte[] Header = new byte[12];
                    if (Stream.Read(Header, 0, 12) != 12) return 0;
                    if (Encoding.ASCII.GetString(Header, 0, 4) != "RIFF" || Encoding.ASCII.GetString(Header, 8, 4) != "WAVE") return 0;

                    byte[] ChunkHeader = new byte[8];
                    while (Stream.Position + 8 <= Stream.Length) {
                        if (Stream.Read(ChunkHeader, 0, 8) != 8) break;
                        string ChunkId = Encoding.ASCII.GetString(ChunkHeader, 0, 4);
                        int ChunkSize = BitConverter.ToInt32(ChunkHeader, 4);
                        if (ChunkSize < 0) break;

                        if (ChunkId == "fmt ") {
                            if (ChunkSize > 4096) break;
                            byte[] Fmt = new byte[ChunkSize];
                            int Read = 0;
                            while (Read < ChunkSize) {
                                int Got = Stream.Read(Fmt, Read, ChunkSize - Read);
                                if (Got <= 0) break;
                                Read += Got;
                            }
                            if (Read < 16) return 0;
                            int FormatTag = BitConverter.ToUInt16(Fmt, 0);
                            int ContainerBits = BitConverter.ToUInt16(Fmt, 14);
                            //WAVE_FORMAT_EXTENSIBLE：偏移 18 处是“有效位”
                            if (FormatTag == 0xFFFE && Read >= 20) {
                                int ValidBits = BitConverter.ToUInt16(Fmt, 18);
                                if (ValidBits > 0) return ValidBits;
                            }
                            return ContainerBits;
                        }

                        //跳过这个块（块长度为奇数时还有 1 字节填充）
                        Stream.Seek(ChunkSize + (ChunkSize % 2), System.IO.SeekOrigin.Current);
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine("ReadWavBitDepth(" + FileFullName + "): " + ex.Message);
            }
            return 0;
        }

        /// <summary>
        /// 把 WAV 的位深提升到目标值，输出**普通 PCM**（WAVE_FORMAT_PCM, tag=1, 16 字节 fmt）格式。
        ///
        /// 为什么必须自己写、而不是用 ffmpeg：
        ///   ffmpeg 对 24 bit PCM 一律写 WAVE_FORMAT_EXTENSIBLE（tag=0xFFFE，40 字节 fmt），
        ///   而 Surcode MLP Encoder 是 2003 年的软件，不认识 EXTENSIBLE 头，
        ///   会直接判为 "Invalid Wave File" 并拒绝编码。eac3to 写的是普通 PCM，所以 Surcode 才有得读。
        ///
        /// 16 bit → 24 bit 就是把每个样本左移 8 位补零，不经过任何浮点运算，数据完全无损。
        /// </summary>
        private static void ConvertWavBitDepth(string SourceFullName, string DestFullName, int TargetBits) {
            int SourceContainerBits, SourceValidBits, Channels, SampleRate, SourceBytesPerSample;
            long DataOffset, DataSize;
            MainForm.ReadWavLayout(SourceFullName, out SourceContainerBits, out SourceValidBits, out Channels,
                out SampleRate, out DataOffset, out DataSize, out SourceBytesPerSample);

            int SourceBits = (SourceValidBits > 0) ? SourceValidBits : SourceContainerBits;
            //没有 20 bit 的容器，20 bit 目标也装进 24 bit（和 eac3to 的做法一致）
            int OutputBits = (TargetBits <= 16) ? 16 : 24;
            int OutputBytesPerSample = OutputBits / 8;

            if (SourceBytesPerSample <= 0 || SourceBytesPerSample > 4) {
                throw new Exception("Unsupported WAV sample size: " + SourceBytesPerSample + " bytes (" + SourceFullName + ")");
            }

            //本函数只做“升位深”（左移补零，严格无损）。
            //降位深会丢精度、需要重新抖动，而且本程序里从不需要它
            //（eac3to 的 -downNN 已经负责降位深），所以这里明确拒绝，
            //绝不能静默截断成错误的样本。
            if (OutputBits < SourceBits) {
                throw new Exception("Refusing to reduce bit depth (" + SourceBits + " -> " + OutputBits + " bit): "
                    + "use eac3to's -down option instead. File: " + SourceFullName);
            }

            //每个样本要左移多少位才能到目标位深
            int Shift = OutputBits - SourceBits;

            int BlockAlign = Channels * OutputBytesPerSample;
            long OutputDataSize = (DataSize / (Channels * SourceBytesPerSample)) * (long)BlockAlign;

            using (System.IO.FileStream In = new System.IO.FileStream(SourceFullName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read)) {
                using (System.IO.BinaryWriter Out = new System.IO.BinaryWriter(new System.IO.FileStream(DestFullName, System.IO.FileMode.Create))) {
                    //普通 PCM 头，和 eac3to 写出来的一模一样
                    Out.Write(Encoding.ASCII.GetBytes("RIFF"));
                    Out.Write((int)(36 + OutputDataSize));
                    Out.Write(Encoding.ASCII.GetBytes("WAVE"));
                    Out.Write(Encoding.ASCII.GetBytes("fmt "));
                    Out.Write(16);
                    Out.Write((short)1);                 //WAVE_FORMAT_PCM
                    Out.Write((short)Channels);
                    Out.Write(SampleRate);
                    Out.Write(SampleRate * BlockAlign);  //byte rate
                    Out.Write((short)BlockAlign);
                    Out.Write((short)OutputBits);
                    Out.Write(Encoding.ASCII.GetBytes("data"));
                    Out.Write((int)OutputDataSize);

                    In.Seek(DataOffset, System.IO.SeekOrigin.Begin);

                    const int BufferFrames = 65536;
                    byte[] InBuffer = new byte[BufferFrames * Channels * SourceBytesPerSample];
                    byte[] OutBuffer = new byte[BufferFrames * BlockAlign];
                    long Remaining = DataSize;

                    while (Remaining > 0) {
                        int Want = (int)Math.Min(InBuffer.Length, Remaining);
                        //按整帧读，避免最后一个样本被截断
                        int FrameBytes = Channels * SourceBytesPerSample;
                        Want -= Want % FrameBytes;
                        if (Want <= 0) break;

                        int Got = 0;
                        while (Got < Want) {
                            int n = In.Read(InBuffer, Got, Want - Got);
                            if (n <= 0) break;
                            Got += n;
                        }
                        if (Got < Want) Want -= (Want - Got) % FrameBytes;
                        if (Want <= 0) break;

                        int Frames = Want / FrameBytes;
                        int OutPos = 0;
                        for (int f = 0; f < Frames; ++f) {
                            for (int c = 0; c < Channels; ++c) {
                                int o = (f * Channels + c) * SourceBytesPerSample;
                                int Value;
                                //读成有符号整数（8 bit WAV 是无符号的，要减 128）
                                if (SourceBytesPerSample == 1) Value = InBuffer[o] - 128;
                                else if (SourceBytesPerSample == 2) Value = (short)(InBuffer[o] | (InBuffer[o + 1] << 8));
                                else if (SourceBytesPerSample == 3) {
                                    Value = InBuffer[o] | (InBuffer[o + 1] << 8) | (InBuffer[o + 2] << 16);
                                    if (Value >= 0x800000) Value -= 0x1000000;
                                }
                                else {
                                    Value = InBuffer[o] | (InBuffer[o + 1] << 8) | (InBuffer[o + 2] << 16) | (InBuffer[o + 3] << 24);
                                }

                                //8 bit 是无符号的，还原成有符号后位深相当于 8 bit
                                //左移扩展到目标位深（纯位操作，无损）
                                int Scaled = (Shift == 0) ? Value : (Value << Shift);
                                if (OutputBytesPerSample == 2) {
                                    OutBuffer[OutPos++] = (byte)(Scaled & 0xFF);
                                    OutBuffer[OutPos++] = (byte)((Scaled >> 8) & 0xFF);
                                }
                                else {
                                    OutBuffer[OutPos++] = (byte)(Scaled & 0xFF);
                                    OutBuffer[OutPos++] = (byte)((Scaled >> 8) & 0xFF);
                                    OutBuffer[OutPos++] = (byte)((Scaled >> 16) & 0xFF);
                                }
                            }
                        }
                        Out.Write(OutBuffer, 0, OutPos);
                        Remaining -= Want;
                    }
                }
            }
        }

        /// <summary>
        /// 读取 WAV 的格式布局：容器位深、有效位深、声道数、采样率、data 块位置与长度。
        /// 优先采用 WAVE_FORMAT_EXTENSIBLE 的“有效位”字段。
        /// </summary>
        private static void ReadWavLayout(string FileFullName, out int ContainerBits, out int ValidBits, out int Channels,
            out int SampleRate, out long DataOffset, out long DataSize, out int BytesPerSample) {
            ContainerBits = 0; ValidBits = 0; Channels = 0; SampleRate = 0;
            DataOffset = -1; DataSize = 0; BytesPerSample = 0;

            using (System.IO.FileStream Stream = new System.IO.FileStream(FileFullName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)) {
                byte[] Header = new byte[12];
                if (Stream.Read(Header, 0, 12) != 12) throw new Exception("Not a WAV file: " + FileFullName);
                if (Encoding.ASCII.GetString(Header, 0, 4) != "RIFF" || Encoding.ASCII.GetString(Header, 8, 4) != "WAVE") {
                    throw new Exception("Not a WAV file: " + FileFullName);
                }

                byte[] ChunkHeader = new byte[8];
                while (Stream.Position + 8 <= Stream.Length) {
                    if (Stream.Read(ChunkHeader, 0, 8) != 8) break;
                    string ChunkId = Encoding.ASCII.GetString(ChunkHeader, 0, 4);
                    int ChunkSize = BitConverter.ToInt32(ChunkHeader, 4);
                    if (ChunkSize < 0) break;

                    if (ChunkId == "fmt ") {
                        if (ChunkSize > 4096) throw new Exception("Suspicious fmt chunk in " + FileFullName);
                        byte[] Fmt = new byte[ChunkSize];
                        int Read = 0;
                        while (Read < ChunkSize) {
                            int Got = Stream.Read(Fmt, Read, ChunkSize - Read);
                            if (Got <= 0) break;
                            Read += Got;
                        }
                        if (Read < 16) throw new Exception("Truncated fmt chunk in " + FileFullName);
                        int FormatTag = BitConverter.ToUInt16(Fmt, 0);
                        Channels = BitConverter.ToUInt16(Fmt, 2);
                        SampleRate = BitConverter.ToInt32(Fmt, 4);
                        ContainerBits = BitConverter.ToUInt16(Fmt, 14);
                        ValidBits = ContainerBits;
                        if (FormatTag == 0xFFFE && Read >= 20) {
                            int v = BitConverter.ToUInt16(Fmt, 18);
                            if (v > 0) ValidBits = v;
                        }
                        //块体已经读完了，位置本来就正确，不能再跳一次
                        if (ChunkSize % 2 == 1) Stream.Seek(1, System.IO.SeekOrigin.Current);
                        continue;
                    }

                    if (ChunkId == "data") {
                        DataOffset = Stream.Position;
                        DataSize = ChunkSize;
                        //data 块已经定位到了，后面的内容不需要再看
                        break;
                    }

                    //不认识的块：整块跳过（奇数长度还有 1 字节填充）
                    Stream.Seek(ChunkSize + (ChunkSize % 2), System.IO.SeekOrigin.Current);
                }
            }

            if (ContainerBits <= 0 || Channels <= 0 || DataOffset < 0) {
                throw new Exception("Incomplete WAV header: " + FileFullName);
            }
            BytesPerSample = ContainerBits / 8;
            //data 块声明的长度超出文件时不信任它
            long FileLength = new System.IO.FileInfo(FileFullName).Length;
            if (DataOffset + DataSize > FileLength) DataSize = FileLength - DataOffset;
        }

        /// <summary>
        /// 把 eac3to 拆分出来的各声道 wav 提升到目标位深（原地替换）。
        /// 只处理 CreateSsfFiles 会用到的那几个声道后缀，避免误伤别的文件。
        /// </summary>
        private void UpconvertProducedWavFiles(int Index, int TargetBits) {
            string Directory = this.Page5TempTextbox.Text.TrimEnd('\\') + '\\';
            string[] ChannelSuffixes = { ".L.wav", ".R.wav", ".C.wav", ".LFE.wav", ".SL.wav", ".SR.wav", ".S.wav" };

            bool Reported = false;
            foreach (string Suffix in ChannelSuffixes) {
                string WavFullName = Directory + this.EncodeNames[Index] + Suffix;
                if (!System.IO.File.Exists(WavFullName)) continue;

                int CurrentBits = MainForm.ReadWavBitDepth(WavFullName);
                if (CurrentBits <= 0) {
                    //读不出来就不动它，免得把好文件弄坏
                    Debug.WriteLine("Can not read bit depth of " + WavFullName + ", leaving it alone.");
                    continue;
                }
                if (CurrentBits >= TargetBits) continue; //eac3to 已经给到目标位深了

                if (!Reported) {
                    Reported = true;
                    this.Page6eac3toBackgroundWorker.ReportProgress(Index * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, MainFormCodeStrings.Page6eac3toBackgroundWorkerDoWorkUpconvertingString, this.Files[Index, 0], false));
                }

                string TempFullName = WavFullName + ".up.wav";
                MainForm.TryDeleteFile(TempFullName);
                try {
                    MainForm.ConvertWavBitDepth(WavFullName, TempFullName, TargetBits);
                }
                catch (Exception ex) {
                    MainForm.TryDeleteFile(TempFullName);
                    throw new Exception(MainFormCodeStrings.UpconvertBitDepthFailedString + Environment.NewLine + ex.Message);
                }

                //临时文件是自己刚生成的，正常情况下不会有人占用
                if (!this.TryDeleteFileWithRetry(WavFullName, MainForm.ShortFileWaitMilliseconds)) {                    MainForm.TryDeleteFile(TempFullName);
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, MainFormCodeStrings.WaitingForFileString) + Environment.NewLine + WavFullName);
                }
                System.IO.File.Move(TempFullName, WavFullName);
                Debug.WriteLine("Upconverted " + CurrentBits + "->" + TargetBits + " bit: " + WavFullName);
            }
        }

        private static void CreateSsfFiles(string FileName, string WavFilePath, string SsfFilePath, string MlpFilePath) {

            using (System.IO.BinaryWriter SsfFileWriter = new System.IO.BinaryWriter(new System.IO.FileStream
                (SsfFilePath + '\\' + FileName + ".ssf", System.IO.FileMode.Create))) {
                byte[] FileHeadBin = { 7, 83, 117, 114, 99, 111, 100, 101, 3, 49, 46, 48, 32, 32, 32, 32, 32, 32, 32,
                32, 1, 32, 32, 32, 1, 32, 32, 32, 1, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 1, 32, 32, 32, 1,
                32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 15, 32, 32, 32, 32, 32, 32, 32, 32,
                32, 32, 32, 1, 32, 32, 32, 27, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32,
                32, 32, 32, 7, 32, 32, 32, 7, 32, 32, 32 };
                byte[] FileEndBin = { 0, 0, 0 };

                bool FLExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".L.wav");
                bool FRExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".R.wav");
                bool CExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".C.wav");
                bool SLExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".SL.wav");
                bool SRExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".SR.wav");
                bool LfeExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".LFE.wav");
                bool SExists = System.IO.File.Exists(WavFilePath + "\\" + FileName + ".S.wav");

                //关键：.ssf 里的路径必须按系统 ANSI 代码页写出。
                //.NET Framework 的 Encoding.Default 就是系统 ANSI 代码页，而 .NET 10 的 Encoding.Default 是 UTF-8，
                //直接沿用它会让 Surcode 读到乱码路径。
                byte[] WavFilePathBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath.TrimEnd('\\') + '\\');
                byte[] MlpFilePathBin = LegacyTextEncoding.Ansi.GetBytes(MlpFilePath.TrimEnd('\\') + '\\');
                byte[] MlpFileFullNameBin = LegacyTextEncoding.Ansi.GetBytes(MlpFilePath.TrimEnd('\\') + '\\' + FileName + ".mlp");

                byte[] ModeBin, FLBin, FRBin, SLBin, SRBin, CBin, LfeBin;

                if (CExists && (!(FLExists || FRExists || LfeExists || SLExists || SRExists)))
                    ModeBin = new byte[] { 0 };
                else if (FLExists && FRExists && (!(LfeExists || SLExists || SRExists || CExists)))
                    ModeBin = new byte[] { 1 };
                else if (FLExists && FRExists && SExists && (!(CExists || LfeExists || SLExists || SRExists)))
                    ModeBin = new byte[] { 2 };
                else if (FLExists && FRExists && SLExists && SRExists && (!(CExists || LfeExists)))
                    ModeBin = new byte[] { 3 };
                else if (FLExists && FRExists && LfeExists && (!(CExists || SLExists || SRExists)))
                    ModeBin = new byte[] { 4 };
                else if (FLExists && FRExists && LfeExists && SExists && (!CExists || SLExists || SRExists))
                    ModeBin = new byte[] { 5 };
                else if (FLExists && FRExists && LfeExists && SLExists && SRExists && (!(CExists)))
                    ModeBin = new byte[] { 6 };
                else if (CExists && FLExists && FRExists && (!(LfeExists || SLExists || SRExists)))
                    ModeBin = new byte[] { 7 };
                else if (CExists && FLExists && FRExists && SExists && (!(LfeExists || SLExists || SRExists)))
                    ModeBin = new byte[] { 8 };
                else if (FLExists && FRExists && CExists && SLExists && SRExists && (!LfeExists))
                    ModeBin = new byte[] { 9 };
                else if (FLExists && FRExists && CExists && LfeExists && (!(SLExists || SRExists)))
                    ModeBin = new byte[] { 10 };
                else if (FLExists && FRExists && CExists && LfeExists && SExists && (!(SLExists || SRExists)))
                    ModeBin = new byte[] { 11 };
                else if (FLExists && FRExists && CExists && LfeExists && SLExists && SRExists)
                    ModeBin = new byte[] { 12 };
                else if (FLExists && FRExists && CExists && SExists && (!(LfeExists || SRExists || SLExists)))
                    ModeBin = new byte[] { 13 };
                else if (FLExists && FRExists && CExists && SLExists && SRExists && (!LfeExists))
                    ModeBin = new byte[] { 14 };
                else if (FLExists && FRExists && CExists && LfeExists && (!(SLExists || SRExists)))
                    ModeBin = new byte[] { 15 };
                else if (FLExists && FRExists && CExists && LfeExists && SExists && (!(SLExists || SRExists)))
                    ModeBin = new byte[] { 16 };
                else if (FLExists && FRExists && CExists && LfeExists && SLExists && SRExists)
                    ModeBin = new byte[] { 17 };
                else if (FLExists && FRExists && LfeExists && SLExists && SRExists && (!CExists))
                    ModeBin = new byte[] { 18 };
                else if (FLExists && FRExists && CExists && SLExists && SRExists && (!LfeExists))
                    ModeBin = new byte[] { 19 };
                else if (FLExists && FRExists && CExists && LfeExists && SLExists && SRExists)
                    ModeBin = new byte[] { 20 };
                else {
                    throw new Exception(MainFormCodeStrings.CreateSsfFilesExceptionString);
                }

                if (FLExists)
                    FLBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".L.wav");
                else
                    FLBin = new byte[] { 0 };

                if (FRExists)
                    FRBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".R.wav");
                else
                    FRBin = new byte[] { 0 };

                if (SLExists)
                    SLBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".SL.wav");
                else if (SExists)
                    SLBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".S.wav");
                else
                    SLBin = new byte[] { 0 };

                if (SRExists)
                    SRBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".SR.wav");
                else
                    SRBin = new byte[] { 0 };

                if (LfeExists)
                    LfeBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".LFE.wav");
                else
                    LfeBin = new byte[] { 0 };

                if (CExists)
                    CBin = LegacyTextEncoding.Ansi.GetBytes(WavFilePath + "\\" + FileName + ".C.wav");
                else
                    CBin = new byte[] { 0 };

                ValidateSsfFieldLength("左声道 WAV", FLBin);
                ValidateSsfFieldLength("右声道 WAV", FRBin);
                ValidateSsfFieldLength("环绕左声道 WAV", SLBin);
                ValidateSsfFieldLength("环绕右声道 WAV", SRBin);
                ValidateSsfFieldLength("中置声道 WAV", CBin);
                ValidateSsfFieldLength("低频声道 WAV", LfeBin);
                ValidateSsfFieldLength("WAV 临时目录", WavFilePathBin);
                ValidateSsfFieldLength("MLP 输出目录", MlpFilePathBin);
                ValidateSsfFieldLength("MLP 输出文件", MlpFileFullNameBin);

                SsfFileWriter.Write(FileHeadBin);
                SsfFileWriter.Write(Convert.ToByte(FLBin.Length));
                SsfFileWriter.Write(FLBin);
                SsfFileWriter.Write(Convert.ToByte(FRBin.Length));
                SsfFileWriter.Write(FRBin);
                SsfFileWriter.Write(Convert.ToByte(SLBin.Length));
                SsfFileWriter.Write(SLBin);
                SsfFileWriter.Write(Convert.ToByte(SRBin.Length));
                SsfFileWriter.Write(SRBin);
                SsfFileWriter.Write(Convert.ToByte(CBin.Length));
                SsfFileWriter.Write(CBin);
                SsfFileWriter.Write(Convert.ToByte(LfeBin.Length));
                SsfFileWriter.Write(LfeBin);
                SsfFileWriter.Write(Convert.ToByte(WavFilePathBin.Length));
                SsfFileWriter.Write(WavFilePathBin);
                SsfFileWriter.Write(Convert.ToByte(MlpFilePathBin.Length));
                SsfFileWriter.Write(MlpFilePathBin);
                SsfFileWriter.Write(Convert.ToByte(MlpFileFullNameBin.Length));
                SsfFileWriter.Write(MlpFileFullNameBin);
                SsfFileWriter.Write(ModeBin);
                SsfFileWriter.Write(FileEndBin);
                //SsfFileWriter.Close();

            }

        }

        private static void ValidateSsfFieldLength(string FieldName, byte[] Value) {
            if (Value.Length > Byte.MaxValue) {
                throw new Exception(FieldName + " 的 ANSI 路径长度为 " +
                    Value.Length.ToString(CultureInfo.InvariantCulture) +
                    " 字节，超过 Surcode SSF 格式的 255 字节上限。请使用更短的临时或输出目录。");
            }
        }

        private void Page6eac3toBackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            for (int i = 0; i < this.Page2ListView.Items.Count; ++i) {
                if (this.Page6eac3toBackgroundWorker.CancellationPending) { e.Cancel = true; return; } //check the cancel button

                string Arguments = string.Empty;
                //Generate eac3to arguments
                if (this.Page4CheckBox.Checked && this.Page4eac3toCheckBox.Checked && this.Page4IgnoreCheckBox.Checked) {
                    Arguments = this.Page4eac3toTextBox.Text;
                }
                else {
                    //1. Resampling
                    if (this.Page3AlwaysResampleRadioButton.Checked) {
                        Arguments += " -resampleTo" + GetChosenSamplingRate().ToString(CultureInfo.InvariantCulture);
                    }
                    else if (this.Page3OnlyResampleRadioButton.Checked) {
                        if (!(this.Files[i, 3] == "44100" || this.Files[i, 3] == "48000" || this.Files[i, 3] == "88200" || this.Files[i, 3] == "96000" || this.Files[i, 3] == "176400" || this.Files[i, 3] == "192000")) {
                            Arguments += " -resampleTo" + GetChosenSamplingRate().ToString(CultureInfo.InvariantCulture);
                        }

                    }
                    //2. Rebit

                    //Experimental!

                    //eac3to 的 -downNN 只能降低位深、无法提升；需要提升时由下面的
                    //UpconvertProducedWavFiles 对 eac3to 拆出的 wav 自己重写。
                    //“不转换任何文件” 即不传任何参数，无需在这里处理。
                    int RebitChosen;
                    bool RebitOnlyNotAllowed;
                    MainForm.RebitDecision Rebit;
                    if (this.GetRebitSetting(out RebitChosen, out RebitOnlyNotAllowed)) {
                        Rebit = this.DecideRebit(i, RebitChosen, RebitOnlyNotAllowed, this.WillEac3toResample(this.Files[i, 3]));
                    }
                    else {
                        Rebit = new MainForm.RebitDecision();
                    }

                    Arguments += Rebit.Arguments;

                    if (!String.IsNullOrEmpty(Rebit.Message)) {
                        this.Page6eac3toBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, Rebit.Message, this.Files[i, 0], false));
                    }

                    //3. Downmix

                    if (this.Page3Down6RadioButton.Checked) {
                        Arguments += " -down6";
                    }
                    else if (this.Page3DownDplRadioButton.Checked) {
                        Arguments += " -downDpl";
                        if (this.Page3DplMixlfeCheckBox.Checked) Arguments += " -mixlfe";
                        if (this.Page3DplPhaseShiftCheckBox.Checked) Arguments += " -phaseShift";
                    }
                    else if (this.Page3DownStereoRadioButton.Checked) {
                        Arguments += " -downStereo";
                    }

                    if (this.Page4CheckBox.Checked && this.Page4eac3toCheckBox.Checked) Arguments += " " + this.Page4eac3toTextBox.Text;

                    if (this.Page6eac3toBackgroundWorker.CancellationPending) { e.Cancel = true; return; } //check the cancel button

                    int BigWaitTime = Convert.ToInt32(this.Page4D.Value + this.Page4E.Value * Convert.ToInt32(this.Files[i, 2]), CultureInfo.CurrentCulture);
                    int BigWaitInterval = decimal.ToInt32(this.Page4F.Value);
                    int BigWaitTimes = BigWaitTime * 1000 / BigWaitInterval + 1;
                    Int32 RetryTimes = decimal.ToInt32(this.Page4C.Value);
                    for (int j = 1; j <= RetryTimes; ++j) {
                        try {
                            Debug.WriteLine("i=" + i + " j=" + j);

                            if (this.Page6eac3toBackgroundWorker.CancellationPending) { e.Cancel = true; return; } //check the cancel button

                            using (Process eac3toProcess = new Process()) {
                                //eac3toProcess.StartInfo.RedirectStandardError = true;
                                //eac3toProcess.StartInfo.RedirectStandardOutput = true;
                                eac3toProcess.StartInfo.UseShellExecute = true;
                                eac3toProcess.StartInfo.FileName = this.Page1eac3toTextbox.Text;
                                Debug.WriteLine("eac3toProcess.StartInfo.FileName = " + eac3toProcess.StartInfo.FileName);
                                eac3toProcess.StartInfo.Arguments = "\"" + this.Files[i, 5] + "\" \"" + this.Page5TempTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + ".wavs\"" + Arguments;
                                eac3toProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                Debug.WriteLine("eac3toProcess.StartInfo.Arguments = " + eac3toProcess.StartInfo.Arguments);

                                eac3toProcess.Start();

                                this.Page6eac3toBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, MainFormCodeStrings.Page6eac3toBackgroundWorkerDoWorkEac3toWorkingString, this.Files[i, 0], false));

                                bool eac3toExited = false;
                                for (int k = 0; k < BigWaitTimes; ++k) {
                                    eac3toExited = eac3toProcess.WaitForExit(BigWaitInterval);
                                    if (this.Page6eac3toBackgroundWorker.CancellationPending) { e.Cancel = true; return; } //check the cancel button
                                    if (eac3toExited) break;
                                }
                                if (!eac3toExited) {
                                    throw new Exception(MainFormCodeStrings.Page6eac3toBackgroundWorkerDoWorkEac3toTimeoutString);
                                }

                                if (this.Page6eac3toBackgroundWorker.CancellationPending) { e.Cancel = true; return; } //check the cancel button

                                //位深提升必须放在 eac3to 之后。
                                //如果先把源文件左移补零升成 24 bit 再交给 eac3to，eac3to 会检测到
                                //“24 bit 容器里低字节全是 0”并主动剥掉多余的零字节，输出又变回 16 bit
                                //（日志里的 "Superfluous zero bytes detected, will be stripped"）。
                                //改成对 eac3to 拆分出来的 wav 做位深提升，就不存在这个问题，
                                //而且自己重写 WAV 不必依赖任何外部工具对各种源格式的支持。
                                if (Rebit.NeedUpconvert) {
                                    this.UpconvertProducedWavFiles(i, Rebit.UpconvertBits);
                                }

                                //Debug.WriteLine("StandardOutput:");
                                //Debug.WriteLine(eac3toProcess.StandardOutput.ReadToEnd());
                                //Debug.WriteLine("StandardError:");
                                //Debug.WriteLine(eac3toProcess.StandardError.ReadToEnd());
                                try {
                                    CreateSsfFiles(this.EncodeNames[i], this.Page5TempTextbox.Text, this.Page5TempTextbox.Text, this.Page5SaveTextbox.Text);
                                }
                                catch (Exception ex) {
                                    string Message = ex.Message;
                                    if (System.IO.File.Exists(this.Page5TempTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + " - Log.txt")) {
                                        using (System.IO.StreamReader Reader = new System.IO.StreamReader(this.Page5TempTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + " - Log.txt", LegacyTextEncoding.Ansi)) {
                                            Message += Environment.NewLine + "------------------------------------------------------------------------------" + Environment.NewLine + Reader.ReadToEnd();
                                            // Reader.Close();
                                        }

                                    }

                                    throw new Exception(Message);
                                }

                                this.eac3toOK[i] = true;
                                this.eac3toFailed[i] = false;

                            }

                            break;
                        }
                        catch (Exception ex) {
                            //1.Report
                            this.Page6eac3toBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, ex.Message, this.Files[i, 0], true));

                            //2. eac3toError boolean
                            if (j == RetryTimes) {
                                this.eac3toFailed[i] = true;
                                this.eac3toOK[i] = false;
                            }
                            else {
                                Thread.Sleep(BigWaitInterval);
                            }

                        }
                    }

                }

            }

        }
        private static void ExecuteReports(Page6WorkerReportArgument Report, ListView Page6ListView) {
            if (Report.Order == Page6WorkerReportArgument.Orders.MessageBox) {
                MessageBox.Show(Report.Message, MainFormCodeStrings.ExecuteReportsMessageString, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else if (Report.Order == Page6WorkerReportArgument.Orders.Add) {
                ListViewItem Item = new ListViewItem(new string[] { Report.Time, Report.FileName, Report.Message });
                if (Report.Stress) {
                    Item.ForeColor = Color.Red;
                }
                Page6ListView.Items.Add(Item);

            }
        }

        private void Page6eac3toBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (!e.Cancelled) {
                this.Page6eac3toProgressBar.Value = this.Page6eac3toProgressBar.Maximum;
            };
            this.eac3Processing = false;

            Page6BackgroundWorkers_RunWorkerCompleted(e);
        }

        private void Page6Listviews_SelectedIndexChanged(object sender, EventArgs e) {
            ListView Page6ListView = sender as ListView;
            if (Page6ListView.SelectedItems.Count > 0) {
                this.Page6DetailsTextbox.ForeColor = Page6ListView.SelectedItems[0].ForeColor;
                if (!String.IsNullOrEmpty(Page6ListView.SelectedItems[0].SubItems[1].Text)) {
                    this.Page6DetailsTextbox.Text = "[" + Page6ListView.SelectedItems[0].SubItems[1].Text + "] - ";
                }
                else {
                    this.Page6DetailsTextbox.Text = string.Empty;
                }
                this.Page6DetailsTextbox.Text += Page6ListView.SelectedItems[0].SubItems[0].Text;
                this.Page6DetailsTextbox.Text += Environment.NewLine + Page6ListView.SelectedItems[0].SubItems[2].Text;
            }
            else if (this.Page6eac3toListView.SelectedItems.Count == 0 && this.Page6SurcodeListView.SelectedItems.Count == 0) {
                this.Page6DetailsTextbox.ForeColor = SystemColors.WindowText;
                this.Page6DetailsTextbox.Text = MainFormCodeStrings.Page6ListviewsSelectedIndexChangedDefaultTextString;
            }
        }

        private void Page6eac3toBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            this.Page6eac3toProgressBar.Value = e.ProgressPercentage;
            MainForm.ExecuteReports(e.UserState as Page6WorkerReportArgument, this.Page6eac3toListView);
        }

        private void Page6SurcodeBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            this.Page6SurcodeProgressBar.Value = e.ProgressPercentage;
            MainForm.ExecuteReports(e.UserState as Page6WorkerReportArgument, this.Page6SurcodeListView);
            Debug.WriteLine("Progress = " + e.ProgressPercentage);
        }

        private void Page6SurcodeBackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            try {
                this.EncodeAllFilesWithSurcode(e);
            }
            finally {
                //所有文件处理完之后才统一改名：此时 Surcode 早已退出、文件句柄已释放，
                //远比“编完一个就立刻改名”可靠（后者常常撞上 Surcode 尚未释放句柄）。
                //放在 finally 里，保证成功、失败、取消三种情况下已生成的 MLP 都能拿回原文件名。
                this.RenameMlpFiles();
            }
        }

        private void EncodeAllFilesWithSurcode(DoWorkEventArgs e) {
            Int32 SmallWaitTimes = Decimal.ToInt32(this.Page4A.Value / this.Page4B.Value) + 1;
            int BigWaitInterval = decimal.ToInt32(this.Page4F.Value);
            int RetryTimes = decimal.ToInt32(this.Page4C.Value);
            Int32 SmallWaitInterval = Decimal.ToInt32(this.Page4B.Value);
            for (int j = 1; j <= RetryTimes; ++j) {
                try {
                    RunSurcode(true);
                    this.Page6SurcodeBackgroundWorker.ReportProgress(0, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkStartSurcodeString, string.Empty, false));
                    break;
                }
                catch (Exception ex) {
                    this.Page6SurcodeBackgroundWorker.ReportProgress(0, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, ex.Message, string.Empty, true));

                    if (j < RetryTimes) {
                        Thread.Sleep(BigWaitInterval);
                    }
                }
            }

            if (this.Page6SurcodeBackgroundWorker.CancellationPending) { e.Cancel = true; return; }//check the cancel button

            for (int i = 0; i < this.Page2ListView.Items.Count; ++i) {
                int BigWaitTime = Convert.ToInt32(this.Page4D.Value + this.Page4E.Value * Int32.Parse(this.Files[i, 2]), CultureInfo.CurrentCulture);
                int BigWaitTimes = BigWaitTime * 1000 / BigWaitInterval + 1;

                if (this.Page6SurcodeBackgroundWorker.CancellationPending) { e.Cancel = true; return; }//check the cancel button
                while (!(this.eac3toOK[i] || this.eac3toFailed[i]))//wait for eac3to
                {
                    Thread.Sleep(BigWaitInterval);
                    if (this.Page6SurcodeBackgroundWorker.CancellationPending) { e.Cancel = true; return; }//check the cancel button

                }

                if (this.eac3toFailed[i]) {
                    this.Page6SurcodeBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkEac3toErrorString, this.Files[i, 0], false));
                    continue;
                }

                if (this.NameReplaced[i]) {
                    this.Page6SurcodeBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, string.Format(CultureInfo.CurrentCulture, MainFormCodeStrings.SurcodeSafeFileNameUsedString, this.EncodeNames[i], this.Files[i, 0]), this.Files[i, 0], false));
                }

                IntPtr hWnd = IntPtr.Zero;
                for (int j = 1; j <= RetryTimes; ++j) {
                    try {
                        hWnd = RunSurcode(false);
                        this.Page6SurcodeBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkStartSurcodeString, this.Files[i, 0], false));

                        if (this.Page6SurcodeBackgroundWorker.CancellationPending) { e.Cancel = true; return; }//check the cancel button

                        //Opreate Surcode
                        AutomationElement SurcodeElement = AutomationElement.FromHandle(hWnd);

                        AutomationElementCollection Menu1Elements = SurcodeElement.FindAll(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem), new PropertyCondition(AutomationElement.AutomationIdProperty, "Item 1")));
                        AutomationElement Menu1Element = null;
                        bool found = false;
                        foreach (AutomationElement MenuElement in Menu1Elements) {
                            if (MenuElement.GetCurrentPropertyValue(AutomationElement.NameProperty).ToString() == "Setup") {
                                Menu1Element = MenuElement;
                                found = true;
                                break;
                            }

                        }
                        if (!found) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotFindSetupMenuString);
                        }

                        try {
                            ExpandCollapsePattern Menu1Pattern = Menu1Element.GetCurrentPattern(ExpandCollapsePattern.Pattern) as ExpandCollapsePattern;
                            Menu1Pattern.Expand();
                        }
                        catch (Exception ex) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotExpandSetupMenuString + Environment.NewLine + ex.Message);
                        }

                        Thread.Sleep(SmallWaitInterval);

                        AutomationElement Menu1OpenElement = Menu1Element.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem), new PropertyCondition(AutomationElement.AutomationIdProperty, "Item 57601")));
                        if (Menu1OpenElement == null || Menu1OpenElement.GetCurrentPropertyValue(AutomationElement.NameProperty).ToString() != "Open...") {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotFindOpenMenuString);
                        }

                        try {
                            InvokePattern Menu1OpenPattern = Menu1OpenElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                            Menu1OpenPattern.Invoke();
                        }
                        catch (Exception ex) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotExpandOpenMenuString + Environment.NewLine + ex.Message);
                        }

                        AutomationElement OpenWindowElement = null;

                        found = false;
                        //MUST WAIT FOR THE DIALOG!
                        for (int k = 0; k < SmallWaitTimes; ++k) {
                            Thread.Sleep(SmallWaitInterval);
                            OpenWindowElement = SurcodeElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "#32770"));
                            found = (OpenWindowElement != null);

                            if (found) break;
                        }

                        if (!found) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkOpenDialogTimeoutString);
                        }

                        AutomationElement TextBoxElement = OpenWindowElement.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1152"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit), new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")));
                        if (TextBoxElement == null) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotFindFilenameString);
                        }
                        AutomationElement OpenButtonElement = OpenWindowElement.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.ClassNameProperty, "Button")));
                        if (OpenButtonElement == null) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotFindOpenButtonString);
                        }

                        ValuePattern TextBoxPattern = TextBoxElement.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                        TextBoxPattern.SetValue(this.Page5TempTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + ".ssf");

                        Thread.Sleep(SmallWaitInterval);
                        try {
                            InvokePattern OpenButtonPattern = OpenButtonElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                            OpenButtonPattern.Invoke();
                        }
                        catch (Exception ex) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotClickOpenButtonString + Environment.NewLine + ex.Message);
                        }

                        Thread.Sleep(SmallWaitInterval);

                        AutomationElement StartButtonElement = SurcodeElement.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1050"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.ClassNameProperty, "Button")));

                        if (StartButtonElement == null || StartButtonElement.GetCurrentPropertyValue(AutomationElement.NameProperty).ToString() != "Encode") {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotFindStartButtonString);
                        }

                        //Surcode 实际输出的文件用 EncodeNames[i] 命名，最终会改回 Files[i, 0]
                        string MlpFileFullName = this.Page5SaveTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + ".mlp";
                        string OriginalMlpFileFullName = this.Page5SaveTextbox.Text.TrimEnd('\\') + '\\' + this.Files[i, 0] + ".mlp";
                        //这两个删除必须容错：上一次尝试残留的文件、或用户正在播放的同名文件都可能被占用
                        this.TryDeleteFileWithRetry(MlpFileFullName, MainForm.ShortFileWaitMilliseconds);
                        if (this.NameReplaced[i]) {
                            this.TryDeleteFileWithRetry(OriginalMlpFileFullName, MainForm.ShortFileWaitMilliseconds);
                        }
                        try {
                            InvokePattern StartButtonPattern = StartButtonElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                            StartButtonPattern.Invoke();
                        }
                        catch (Exception ex) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkCannotClickStartButtonString + Environment.NewLine + ex.Message);
                        }

                        IntPtr LogWindowhWnd = IntPtr.Zero;
                        found = false;
                        for (int k = 0; k < BigWaitTimes; ++k) {
                            Thread.Sleep(BigWaitInterval);
                            if (this.Page6SurcodeBackgroundWorker.CancellationPending) { e.Cancel = true; return; }//check the cancel button
                            LogWindowhWnd = NativeMethods.FindWindowExW(IntPtr.Zero, IntPtr.Zero, "#32770", "MLP Encoder Log File");
                            if (LogWindowhWnd != IntPtr.Zero) {
                                found = true;
                                break;
                            }
                        }

                        if (!found) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkSurcodeTimeoutString);
                        }

                        NativeMethods.SendMessageW(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                        if (!System.IO.File.Exists(MlpFileFullName)) {
                            throw new Exception(MainFormCodeStrings.Page6SurcodeBackgroundWorkerDoWorkSurcodeFailedString);
                        }

                        //含韩文的文件名：Surcode 生成的是临时 ASCII 名。
                        //改名不在这里做——此时 Surcode 可能还没释放句柄。
                        //先记下来，全部文件编完之后由 RenameMlpFiles() 统一改回原名。
                        this.SurcodeSucceeded[i] = true;

                        break;
                    }
                    catch (Exception ex) {
                        this.Page6SurcodeBackgroundWorker.ReportProgress(i * 100 / this.Page2ListView.Items.Count, new Page6WorkerReportArgument(Page6WorkerReportArgument.Orders.Add, ex.Message, this.Files[i, 0], true));

                        if (j < RetryTimes) {
                            Thread.Sleep(BigWaitInterval);
                        }
                        else {
                            this.SurcodeFailed[i] = true;
                            //不要留下临时 ASCII 名的 MLP 文件
                            if (this.NameReplaced[i]) {
                                MainForm.TryDeleteFile(this.Page5SaveTextbox.Text.TrimEnd('\\') + '\\' + this.EncodeNames[i] + ".mlp");
                            }
                        }

                        hWnd = IntPtr.Zero;
                    }
                }

            }

        }

        private void Page6CancelButton_Click(object sender, EventArgs e) {
            if (this.Processing) {
                if (MessageBox.Show(MainFormCodeStrings.Page6CancelButtonClickConfirmTextString, MainFormCodeStrings.Page6CancelButtonClickConfirmCaptionString, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes) {
                    this.Page6eac3toBackgroundWorker.CancelAsync();
                    this.Page6SurcodeBackgroundWorker.CancelAsync();

                    this.Page6CancelButton.Enabled = false;

                }
            }

        }

        private void Page1LanguageButton_Click(object sender, EventArgs e) {
            Form languageForm = new LanguageForm();
            languageForm.ShowDialog();
        }

        private void Page6BackgroundWorkers_RunWorkerCompleted(RunWorkerCompletedEventArgs e) {
            if (!(this.eac3Processing || this.SurcodeProcessing)) {
                this.Processing = false;
                this.Page1Panel.Enabled = this.Page2Panel.Enabled = this.Page3Panel.Enabled = this.Page4Panel.Enabled = this.Page5Panel.Enabled = true;
                this.Page6CancelButton.Enabled = false;

                if (e.Cancelled)
                    System.Media.SystemSounds.Asterisk.Play();
                else {
                    int ErrorFileCount = 0;
                    for (int i = 0; i < this.Page2ListView.Items.Count; ++i) {
                        this.SurcodeFailed[i] = this.SurcodeFailed[i] || this.eac3toFailed[i];
                        if (this.SurcodeFailed[i]) ++ErrorFileCount;
                    }
                    if (this.BatchOptions != null) {
                        Environment.ExitCode = ErrorFileCount == 0 && this.RenameFailureCount == 0 ? 0 : 1;
                        this.BeginInvoke(new Action(this.Close));
                        return;
                    }
                    if (ErrorFileCount == 0) {
                        if (this.RenameFailureCount > 0) {
                            //音频都转换成功了，但有文件因为被占用没能改回原文件名
                            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, MainFormCodeStrings.RenameMlpPartiallyFailedString, this.RenameFailureCount), MainFormCodeStrings.Page6BackgroundWorkersRunWorkerCompletedSuccessCaptionString, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else {
                            MessageBox.Show(MainFormCodeStrings.Page6BackgroundWorkersRunWorkerCompletedSuccessTextString, MainFormCodeStrings.Page6BackgroundWorkersRunWorkerCompletedSuccessCaptionString, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    else {
                        if (MessageBox.Show(ErrorFileCount.ToString(CultureInfo.CurrentCulture) + " " + MainFormCodeStrings.Page6BackgroundWorkersRunWorkerCompletedErrorTextString, MainFormCodeStrings.Page6BackgroundWorkersRunWorkerCompletedErrorCaptionString, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) {
                            this.Page2ListView.BeginUpdate();
                            int k = 0, OriginalCount = this.Page2ListView.Items.Count;
                            for (int i = 0; i < OriginalCount; ++i) {
                                if (!this.SurcodeFailed[i]) {
                                    Debug.WriteLine("Remove: " + this.Page2ListView.Items[i - k].Text + " i=" + i + " k=" + k);
                                    this.Page2ListView.Items.RemoveAt(i - k);
                                    ++k;
                                }
                            }
                            this.Page2ListView.EndUpdate();
                            this.MainTabControl.SelectedIndex = 1;

                        }
                    }

                }

            }
        }

        private void Page6SurcodeBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (!e.Cancelled) {
                this.Page6SurcodeProgressBar.Value = this.Page6SurcodeProgressBar.Maximum;
            };

            this.SurcodeProcessing = false;

            Page6BackgroundWorkers_RunWorkerCompleted(e);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            if (this.Processing) {
                if (MessageBox.Show(MainFormCodeStrings.MainFormFormClosingTextString, MainFormCodeStrings.MainFormFormClosingCaptionString, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
                    e.Cancel = true;
                }
            }
        }
    }
}
