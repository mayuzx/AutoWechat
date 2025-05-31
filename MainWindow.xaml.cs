using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Interop;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using System.Net.Http;
using System.Text.Json;
using CozeApiClient;
using Newtonsoft.Json;
using PaddleOCRSharp;
using System.Linq;
using System.Diagnostics;
using System.Windows.Threading;
using System.Security.Policy;

namespace AutoWe
{
    public class AppSettings
    {
        public string SelectedImagePath { get; set; }
        public string SelectedImagePath1 { get; set; }
        public string SelectedImagePath2 { get; set; }
        public string CozeToken { get; set; }
        public string CozeAppId { get; set; }
        public string CozeWorkflowId { get; set; }
        public int MessageBoundary { get; set; } = 160; // 默认值160
        public int LeftOffset { get; set; } = 550; // 默认值550
        public int RightOffset { get; set; } = 0; // 默认值0
        public int TopOffset { get; set; } = 100; // 默认值100
        public int BottomOffset { get; set; } = 100; // 默认值100
        public string MatchThreshold { get; set; } = "0.85"; // 新增，默认值0.85
        public int ScreenshotLeftOffset { get; set; } = 100;
        public int SendClickYOffset { get; set; } = -100;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const byte VK_CONTROL = 0x11;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_ESCAPE = 0x1B;

        private List<(IntPtr hWnd, string Title, string ClassName)> windowList = new List<(IntPtr, string, string)>();
        private string selectedImagePath = string.Empty;
        private string selectedImagePath1 = string.Empty;
        private string selectedImagePath2 = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isCapturing = false;
        private bool _isOpenCvInitialized = false;
        private readonly HttpClient _httpClient = new HttpClient();
        private const string SETTINGS_FILE = "settings.json";
        private OCRModelConfig config;
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private string adUrl = null;
        private DispatcherTimer adScrollTimer;
        private double adTextWidth = 0;
        private double adCanvasWidth = 0;
        private double adCurrentLeft = 0;
        private IntPtr selectedWindowHandle = IntPtr.Zero;

        public class AdModel
        {
            public string content { get; set; }
            public string url { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();

            // 设置DLL搜索路径
            string dllPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll");
            if (Directory.Exists(dllPath))
            {
                SetDllDirectory(dllPath);
                // 将dll目录添加到PATH环境变量
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!path.Contains(dllPath))
                {
                    Environment.SetEnvironmentVariable("PATH", path + ";" + dllPath);
                }
            }

            btnStart.Click += BtnStart_Click;
            InitializeOpenCv();
            InitializePaddleOCR();
            InitializeCozeClient();
            this.Loaded += MainWindow_Loaded;
            // 设置全局键盘钩子
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            LoadAdBar();
            this.SizeChanged += (s, e) =>
            {
                adCanvasWidth = AdCanvas.ActualWidth > 0 ? AdCanvas.ActualWidth : this.ActualWidth;
            };

            // 初始化窗口列表
            RefreshWindowList();
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, LoadLibrary(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_ESCAPE && _isCapturing)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StopCapture();
                        btnStart.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
                        btnStart.Content = "开始(F4)";
                        Log.Text += "按ESC键停止\n";
                        Log.ScrollToEnd();
                    });
                }
                if (vkCode == 115)
                {
                    if (_isCapturing)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StopCapture();
                            btnStart.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
                            btnStart.Content = "开始(F4)";
                            Log.Text += "按ESC键停止\n";
                            Log.ScrollToEnd();
                        });
                    }
                    else
                    {
                        btnStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        private void InitializeOpenCv()
        {
            try
            {
                // 尝试加载OpenCV
                using (var mat = new Mat())
                {
                    _isOpenCvInitialized = true;
                    Log.Text += "OpenCV初始化成功\n";
                }
            }
            catch (Exception ex)
            {
                _isOpenCvInitialized = false;
                Log.Text += $"OpenCV初始化失败: {ex.Message}\n";
            }
        }

        private void InitializePaddleOCR()
        {
            try
            {
                // 初始化PaddleOCR
                string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inference");
                if (!Directory.Exists(modelPath))
                {
                    Directory.CreateDirectory(modelPath);
                }

                // 检查并解压模型文件
                string[] modelFiles = new string[]
                {
                    "ch_PP-OCRv4_det_infer.tar",
                    "ch_PP-OCRv4_rec_infer.tar",
                    "ch_ppocr_mobile_v2.0_cls_infer.tar"
                };

                foreach (string modelFile in modelFiles)
                {
                    string tarPath = System.IO.Path.Combine(modelPath, modelFile);
                    string extractPath = System.IO.Path.Combine(modelPath, System.IO.Path.GetFileNameWithoutExtension(modelFile));

                    if (File.Exists(tarPath) && !Directory.Exists(extractPath))
                    {
                        try
                        {
                            // 使用Process调用tar命令解压
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "tar",
                                    Arguments = $"-xf \"{tarPath}\" -C \"{modelPath}\"",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    CreateNoWindow = true
                                }
                            };
                            process.Start();
                            process.WaitForExit();

                            if (process.ExitCode != 0)
                            {
                                throw new Exception($"解压{modelFile}失败，退出码：{process.ExitCode}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Text += $"解压{modelFile}失败: {ex.Message}\n";
                            throw;
                        }
                    }
                }

                config = new OCRModelConfig();
                config.det_infer = System.IO.Path.Combine(modelPath, "ch_PP-OCRv4_det_infer");
                config.cls_infer = System.IO.Path.Combine(modelPath, "ch_ppocr_mobile_v2.0_cls_infer");
                config.rec_infer = System.IO.Path.Combine(modelPath, "ch_PP-OCRv4_rec_infer");
                config.keys = System.IO.Path.Combine(modelPath, "ppocr_keys_v1.txt");

                // 检查模型文件是否存在
                if (!Directory.Exists(config.det_infer) || !Directory.Exists(config.cls_infer) ||
                    !Directory.Exists(config.rec_infer) || !File.Exists(config.keys))
                {
                    throw new FileNotFoundException("模型文件不完整，请确保所有模型文件都已正确解压");
                }

                Log.Text += "PaddleOCR初始化成功\n";
            }
            catch (Exception ex)
            {
                Log.Text += $"PaddleOCR初始化失败: {ex.Message}\n";
                Log.Text += $"错误详情: {ex.ToString()}\n";
                Log.Text += $"当前目录: {AppDomain.CurrentDomain.BaseDirectory}\n";
                Log.Text += $"DLL搜索路径: {Environment.GetEnvironmentVariable("PATH")}\n";
            }
        }

        private void InitializeCozeClient()
        {
            try
            {
                //if (!string.IsNullOrEmpty(cozeToken) && !string.IsNullOrEmpty(cozeAppId))
                //{
                //    Log.Text += "Coze客户端初始化成功\n";
                //}
            }
            catch (Exception ex)
            {
                Log.Text += $"Coze客户端初始化失败: {ex.Message}\n";
            }
        }

        private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            if (IsWindowVisible(hWnd))
            {
                StringBuilder title = new StringBuilder(256);
                StringBuilder className = new StringBuilder(256);
                GetWindowText(hWnd, title, 256);
                GetClassName(hWnd, className, 256);
                string windowTitle = title.ToString();
                string windowClass = className.ToString();

                if (!string.IsNullOrEmpty(windowTitle))
                {
                    windowList.Add((hWnd, windowTitle, windowClass));
                }
            }
            return true;
        }

        private void RefreshWindowList()
        {
            windowList.Clear();
            EnumWindows(EnumWindowsCallback, IntPtr.Zero);

            // 获取当前窗口句柄
            IntPtr currentWindow = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // 更新下拉框
            cmbTargetWindow.Items.Clear();
            foreach (var window in windowList)
            {
                // 排除当前窗口
                if (!string.IsNullOrEmpty(window.Title) && window.hWnd != currentWindow)
                {
                    cmbTargetWindow.Items.Add($"{window.Title} ({window.ClassName})");
                }
            }
        }

        private void CmbTargetWindow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTargetWindow.SelectedIndex >= 0)
            {
                string selectedItem = cmbTargetWindow.SelectedItem.ToString();
                var selectedWindow = windowList.FirstOrDefault(w => $"{w.Title} ({w.ClassName})" == selectedItem);
                if (selectedWindow.hWnd != IntPtr.Zero)
                {
                    selectedWindowHandle = selectedWindow.hWnd;
                    Log.Text += $"已选择窗口: {selectedItem}\n";
                    Log.ScrollToEnd();
                }
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (selectedWindowHandle == IntPtr.Zero)
            {
                MessageBox.Show("请先选择目标窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 如果窗口最小化，先恢复
            if (IsIconic(selectedWindowHandle))
            {
                ShowWindow(selectedWindowHandle, SW_RESTORE);
                System.Threading.Thread.Sleep(100);
            }
            // 激活窗口
            SetForegroundWindow(selectedWindowHandle);

            if (!_isCapturing)
            {
                StartCapture();
                btnStart.Content = "停止(F4)";
                btnStart.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                StopCapture();
                btnStart.Content = "开始(F4)";
                btnStart.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
            }
        }

        private void StartCapture()
        {
            _isCapturing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));
        }

        private void StopCapture()
        {
            _isCapturing = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        private async Task CaptureLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (selectedWindowHandle != IntPtr.Zero)
                    {
                        // 如果窗口最小化，先恢复
                        if (IsIconic(selectedWindowHandle))
                        {
                            ShowWindow(selectedWindowHandle, SW_RESTORE);
                            System.Threading.Thread.Sleep(100);
                        }
                        // 激活窗口
                        bool b = SetForegroundWindow(selectedWindowHandle);

                        // 获取窗口位置
                        RECT rect;
                        GetWindowRect(selectedWindowHandle, out rect);

                        // 先全窗口截图
                        using (Bitmap fullBitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top))
                        {
                            using (Graphics graphics = Graphics.FromImage(fullBitmap))
                            {
                                IntPtr hdcBitmap = graphics.GetHdc();
                                PrintWindow(selectedWindowHandle, hdcBitmap, PW_RENDERFULLCONTENT);
                                graphics.ReleaseHdc(hdcBitmap);
                            }

                            // 读取偏移
                            //int screenshotLeftOffset = 0;
                            //await Dispatcher.InvokeAsync(() =>
                            //{
                            //int.TryParse(txtScreenshotLeftOffset.Text, out screenshotLeftOffset);
                            //});
                            //int cropWidth = fullBitmap.Width - screenshotLeftOffset;
                            //if (cropWidth <= 0) cropWidth = 1;
                            System.Drawing.Rectangle cropRect = new System.Drawing.Rectangle(0, 0, fullBitmap.Width, fullBitmap.Height);
                            using (Bitmap bitmap = fullBitmap.Clone(cropRect, fullBitmap.PixelFormat))
                            {
                                // 保存截图
                                string screenshotPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wechat_screenshot.png");
                                bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);

                                // 如果已选择图片且OpenCV已初始化，进行图片匹配
                                if (!string.IsNullOrEmpty(selectedImagePath) && _isOpenCvInitialized)
                                {
                                    try
                                    {
                                        using (var screenshot = Cv2.ImRead(screenshotPath))
                                        using (var template = Cv2.ImRead(selectedImagePath))
                                        {
                                            if (screenshot != null && template != null)
                                            {
                                                // 图像预处理
                                                using (var screenshotGray = new Mat())
                                                using (var templateGray = new Mat())
                                                {
                                                    // 转换为灰度图
                                                    Cv2.CvtColor(screenshot, screenshotGray, ColorConversionCodes.BGR2GRAY);
                                                    Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGR2GRAY);

                                                    // 尝试多种匹配方法
                                                    double maxVal = 0;
                                                    OpenCvSharp.Point maxLoc = new OpenCvSharp.Point();
                                                    TemplateMatchModes bestMethod = TemplateMatchModes.CCorrNormed;
                                                    {
                                                        using (var result = screenshotGray.MatchTemplate(templateGray, TemplateMatchModes.CCoeffNormed))
                                                        {
                                                            double minVal, currentMaxVal;
                                                            OpenCvSharp.Point minLoc, currentMaxLoc;
                                                            Cv2.MinMaxLoc(result, out minVal, out currentMaxVal, out minLoc, out currentMaxLoc);

                                                            if (currentMaxVal > maxVal)
                                                            {
                                                                maxVal = currentMaxVal;
                                                                maxLoc = currentMaxLoc;
                                                            }
                                                        }
                                                    }

                                                    await Dispatcher.InvokeAsync(() =>
                                                    {
                                                        Log.Text += $"最佳匹配方法: {bestMethod}\n";
                                                        Log.Text += $"当前匹配度: {maxVal:F4}\n";
                                                        Log.Text += $"最佳匹配位置: ({maxLoc.X}, {maxLoc.Y})\n";
                                                        Log.ScrollToEnd();
                                                    });

                                                    double matchThreshold = 0.85;
                                                    try
                                                    {
                                                        await Dispatcher.InvokeAsync(() =>
                                                        {
                                                            double.TryParse(txtMatchThreshold.Text, out matchThreshold);
                                                        });
                                                    }
                                                    catch { }
                                                    if (maxVal > matchThreshold)
                                                    {
                                                        #region 消息定位

                                                        // 计算中心点坐标
                                                        OpenCvSharp.Point center = new OpenCvSharp.Point(
                                                            maxLoc.X + template.Width / 2,
                                                            maxLoc.Y + template.Height / 2
                                                        );

                                                        // 计算屏幕坐标（考虑窗口位置）
                                                        int screenX = rect.Left + center.X;
                                                        int screenY = rect.Top + center.Y;

                                                        int screenshotLeftOffset = 0;
                                                        await Dispatcher.InvokeAsync(() =>
                                                        {
                                                            int.TryParse(txtScreenshotLeftOffset.Text, out screenshotLeftOffset);
                                                        });
                                                        if (center.X > screenshotLeftOffset)
                                                        {
                                                            // 移动鼠标到匹配位置
                                                            SetCursorPos(screenX, screenY);
                                                            // 等待一小段时间确保鼠标移动完成
                                                            await Task.Delay(100, cancellationToken);

                                                            // 执行鼠标点击
                                                            mouse_event(MOUSEEVENTF_LEFTDOWN, screenX, screenY, 0, 0);
                                                            await Task.Delay(50, cancellationToken);
                                                            mouse_event(MOUSEEVENTF_LEFTUP, screenX, screenY, 0, 0);

                                                            await Dispatcher.InvokeAsync(() =>
                                                            {
                                                                Log.Text += $"找到图片，坐标: ({center.X}, {center.Y}), 匹配度: {maxVal:F2}\n";
                                                                Log.Text += $"鼠标移动到屏幕坐标: ({screenX}, {screenY}) 并点击\n";
                                                                Log.ScrollToEnd();
                                                            });

                                                            // 在截图上标记找到的位置
                                                            Cv2.Rectangle(screenshot, maxLoc,
                                                                new OpenCvSharp.Point(maxLoc.X + template.Width, maxLoc.Y + template.Height),
                                                                Scalar.Red, 2);
                                                            Cv2.Circle(screenshot, center, 5, Scalar.Red, -1);

                                                            // 保存带有红色边框的截图
                                                            Cv2.ImWrite(screenshotPath, screenshot);

                                                            #endregion

                                                            // 如果已选择第二张图片，进行第二次匹配
                                                            if (!string.IsNullOrEmpty(selectedImagePath1))
                                                            {
                                                                await Task.Delay(500, cancellationToken); // 等待第一次点击后的界面变化

                                                                // 重新截图
                                                                using (Graphics graphics = Graphics.FromImage(bitmap))
                                                                {
                                                                    IntPtr hdcBitmap = graphics.GetHdc();
                                                                    PrintWindow(selectedWindowHandle, hdcBitmap, PW_RENDERFULLCONTENT);
                                                                    graphics.ReleaseHdc(hdcBitmap);
                                                                }
                                                                bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);

                                                                using (var screenshot1 = Cv2.ImRead(screenshotPath))
                                                                using (var template1 = Cv2.ImRead(selectedImagePath1))
                                                                {
                                                                    if (screenshot1 != null && template1 != null)
                                                                    {
                                                                        using (var result1 = screenshot1.MatchTemplate(template1, TemplateMatchModes.CCoeffNormed))
                                                                        {
                                                                            double minVal1, maxVal1;
                                                                            OpenCvSharp.Point minLoc1, maxLoc1;
                                                                            Cv2.MinMaxLoc(result1, out minVal1, out maxVal1, out minLoc1, out maxLoc1);

                                                                            if (maxVal1 > 0.8)
                                                                            {
                                                                                OpenCvSharp.Point center1 = new OpenCvSharp.Point(
                                                                                    maxLoc1.X + template1.Width / 2,
                                                                                    maxLoc1.Y + template1.Height / 2
                                                                                );

                                                                                int screenX1 = rect.Left + center1.X;
                                                                                int screenY1 = rect.Top + center1.Y;

                                                                                int sendClickYOffset = -100;
                                                                                await Dispatcher.InvokeAsync(() => {
                                                                                    int.TryParse(txtSendClickYOffset.Text, out sendClickYOffset);
                                                                                });
                                                                                SetCursorPos(screenX1, screenY1 + sendClickYOffset);
                                                                                await Task.Delay(100, cancellationToken);

                                                                                mouse_event(MOUSEEVENTF_LEFTDOWN, screenX1, screenY1, 0, 0);
                                                                                await Task.Delay(50, cancellationToken);
                                                                                mouse_event(MOUSEEVENTF_LEFTUP, screenX1, screenY1, 0, 0);

                                                                                await Dispatcher.InvokeAsync(() =>
                                                                                {
                                                                                    Log.Text += $"找到图片1，坐标: ({center1.X}, {center1.Y}), 匹配度: {maxVal1:F2}\n";
                                                                                    Log.Text += $"鼠标移动到屏幕坐标: ({screenX1}, {screenY1}) 并点击\n";
                                                                                    Log.ScrollToEnd();
                                                                                });

                                                                                Cv2.Rectangle(screenshot1, maxLoc1,
                                                                                    new OpenCvSharp.Point(maxLoc1.X + template1.Width, maxLoc1.Y + template1.Height),
                                                                                    Scalar.Blue, 2);
                                                                                Cv2.Circle(screenshot1, center1, 5, Scalar.Blue, -1);
                                                                                Cv2.ImWrite(screenshotPath, screenshot1);

                                                                                // 等待界面变化后发送截图到Coze
                                                                                await Task.Delay(500, cancellationToken);

                                                                                // 获取OCR区域设置
                                                                                int leftOffset = 0, rightOffset = 0, topOffset = 0, bottomOffset = 0;
                                                                                await Dispatcher.InvokeAsync(() =>
                                                                                {
                                                                                    leftOffset = int.Parse(string.IsNullOrEmpty(txtLeftOffset.Text) ? "0" : txtLeftOffset.Text);
                                                                                    rightOffset = int.Parse(string.IsNullOrEmpty(txtRightOffset.Text) ? "0" : txtRightOffset.Text);
                                                                                    topOffset = int.Parse(string.IsNullOrEmpty(txtTopOffset.Text) ? "0" : txtTopOffset.Text);
                                                                                    bottomOffset = int.Parse(string.IsNullOrEmpty(txtBottomOffset.Text) ? "0" : txtBottomOffset.Text);
                                                                                });

                                                                                // 使用OCR识别截图，设置区域偏移
                                                                                string cozeResponse = await ProcessScreenshotWithOCR(screenshotPath,
                                                                                    leftOffset: leftOffset,
                                                                                    rightOffset: rightOffset,
                                                                                    topOffset: topOffset,
                                                                                    bottomOffset: bottomOffset);

                                                                                if (!string.IsNullOrEmpty(cozeResponse))
                                                                                {
                                                                                    // 将cozeResponse内容写入剪贴板
                                                                                    await Dispatcher.InvokeAsync(() =>
                                                                                    {
                                                                                        Clipboard.SetText(cozeResponse);
                                                                                    });

                                                                                    keybd_event(VK_CONTROL, 0, 0, 0);
                                                                                    keybd_event(0x56, 0, 0, 0); // V键
                                                                                    keybd_event(0x56, 0, KEYEVENTF_KEYUP, 0);
                                                                                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                                                                                    await Task.Delay(50, cancellationToken);

                                                                                    SetCursorPos(screenX1, screenY1);
                                                                                    await Task.Delay(100, cancellationToken);

                                                                                    mouse_event(MOUSEEVENTF_LEFTDOWN, screenX1, screenY1, 0, 0);
                                                                                    await Task.Delay(50, cancellationToken);
                                                                                    mouse_event(MOUSEEVENTF_LEFTUP, screenX1, screenY1, 0, 0);

                                                                                    await Dispatcher.InvokeAsync(() =>
                                                                                    {
                                                                                        Log.Text += $"Coze处理结果: {cozeResponse}\n";
                                                                                        Log.ScrollToEnd();
                                                                                    });

                                                                                    // 如果已选择第三张图片，进行第三次匹配
                                                                                    if (!string.IsNullOrEmpty(selectedImagePath2))
                                                                                    {
                                                                                        await Task.Delay(500, cancellationToken); // 等待界面变化

                                                                                        // 重新截图
                                                                                        using (Graphics graphics = Graphics.FromImage(bitmap))
                                                                                        {
                                                                                            IntPtr hdcBitmap = graphics.GetHdc();
                                                                                            PrintWindow(selectedWindowHandle, hdcBitmap, PW_RENDERFULLCONTENT);
                                                                                            graphics.ReleaseHdc(hdcBitmap);
                                                                                        }
                                                                                        bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);

                                                                                        using (var screenshot2 = Cv2.ImRead(screenshotPath))
                                                                                        using (var template2 = Cv2.ImRead(selectedImagePath2))
                                                                                        {
                                                                                            if (screenshot2 != null && template2 != null)
                                                                                            {
                                                                                                using (var result2 = screenshot2.MatchTemplate(template2, TemplateMatchModes.CCoeffNormed))
                                                                                                {
                                                                                                    double minVal2, maxVal2;
                                                                                                    OpenCvSharp.Point minLoc2, maxLoc2;
                                                                                                    Cv2.MinMaxLoc(result2, out minVal2, out maxVal2, out minLoc2, out maxLoc2);

                                                                                                    if (maxVal2 > 0.8)
                                                                                                    {
                                                                                                        OpenCvSharp.Point center2 = new OpenCvSharp.Point(
                                                                                                            maxLoc2.X + template2.Width / 2,
                                                                                                            maxLoc2.Y + template2.Height / 2
                                                                                                        );

                                                                                                        int screenX2 = rect.Left + center2.X;
                                                                                                        int screenY2 = rect.Top + center2.Y;

                                                                                                        SetCursorPos(screenX2, screenY2);
                                                                                                        await Task.Delay(100, cancellationToken);

                                                                                                        mouse_event(MOUSEEVENTF_LEFTDOWN, screenX2, screenY2, 0, 0);
                                                                                                        await Task.Delay(50, cancellationToken);
                                                                                                        mouse_event(MOUSEEVENTF_LEFTUP, screenX2, screenY2, 0, 0);

                                                                                                        await Dispatcher.InvokeAsync(() =>
                                                                                                        {
                                                                                                            Log.Text += $"找到图片2，坐标: ({center2.X}, {center2.Y}), 匹配度: {maxVal2:F2}\n";
                                                                                                            Log.Text += $"鼠标移动到屏幕坐标: ({screenX2}, {screenY2}) 并点击\n";
                                                                                                            Log.ScrollToEnd();
                                                                                                        });

                                                                                                        Cv2.Rectangle(screenshot2, maxLoc2,
                                                                                                            new OpenCvSharp.Point(maxLoc2.X + template2.Width, maxLoc2.Y + template2.Height),
                                                                                                            Scalar.Green, 2);
                                                                                                        Cv2.Circle(screenshot2, center2, 5, Scalar.Green, -1);
                                                                                                        Cv2.ImWrite(screenshotPath, screenshot2);
                                                                                                    }
                                                                                                    else
                                                                                                    {
                                                                                                        await Dispatcher.InvokeAsync(() =>
                                                                                                        {
                                                                                                            Log.Text += $"未找到匹配图片2，最高匹配度: {maxVal2:F2}\n";
                                                                                                            Log.ScrollToEnd();
                                                                                                        });
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                await Dispatcher.InvokeAsync(() =>
                                                                                {
                                                                                    Log.Text += $"未找到匹配图片1，最高匹配度: {maxVal1:F2}\n";
                                                                                    Log.ScrollToEnd();
                                                                                });
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            Log.Text += $"图片匹配失败: {ex.Message}\n";
                                            Log.Text += $"错误详情: {ex.ToString()}\n";
                                            Log.ScrollToEnd();
                                        });
                                    }
                                }

                                await Dispatcher.InvokeAsync(() =>
                                {
                                    Log.Text += $"截图成功: {DateTime.Now:HH:mm:ss}\n";
                                    Log.ScrollToEnd();
                                });
                            }
                        }
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            Log.Text += $"未找到微信窗口: {DateTime.Now:HH:mm:ss}\n";
                            Log.ScrollToEnd();
                        });
                    }
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Log.Text += $"截图失败: {ex.Message} - {DateTime.Now:HH:mm:ss}\n";
                        Log.Text += $"错误详情: {ex.ToString()}\n";
                        Log.ScrollToEnd();
                    });
                }

                // 等待1秒
                await Task.Delay(600, cancellationToken);
            }
        }

        private async Task<string> ProcessScreenshotWithOCR(string imagePath, int leftOffset = 0, int rightOffset = 0, int topOffset = 0, int bottomOffset = 0)
        {
            try
            {

                // 图像预处理
                using (var mat = Cv2.ImRead(imagePath))
                {
                    if (mat.Empty())
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            Log.Text += "无法读取图片\n";
                            Log.ScrollToEnd();
                        });
                        return null;
                    }

                    // 创建ROI区域
                    int width = mat.Width;
                    int height = mat.Height;

                    // 确保偏移量有效
                    leftOffset = Math.Max(0, Math.Min(leftOffset, width - 1));
                    rightOffset = Math.Max(0, Math.Min(rightOffset, width - 1));
                    topOffset = Math.Max(0, Math.Min(topOffset, height - 1));
                    bottomOffset = Math.Max(0, Math.Min(bottomOffset, height - 1));

                    // 计算ROI区域
                    int roiX = leftOffset;
                    int roiY = topOffset;
                    int roiWidth = width - leftOffset - rightOffset;
                    int roiHeight = height - topOffset - bottomOffset;

                    // 确保ROI区域有效
                    if (roiWidth <= 0 || roiHeight <= 0)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            Log.Text += "无效的识别区域\n";
                            Log.ScrollToEnd();
                        });
                        return null;
                    }

                    // 提取ROI区域
                    using (var roi = new Mat(mat, new OpenCvSharp.Rect(roiX, roiY, roiWidth, roiHeight)))
                    {
                        // 保存ROI区域图片
                        string roiPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(imagePath),
                            "roi_" + System.IO.Path.GetFileName(imagePath));
                        Cv2.ImWrite(roiPath, roi);

                        // 等待文件系统释放
                        await Task.Delay(200);
                        // 使用PaddleOCR识别ROI区域
                        var par = new OCRParameter
                        {
                            det_db_thresh = 0.3f,
                            det_db_box_thresh = 0.5f,
                            det_db_unclip_ratio = 2.0f,
                            max_side_len = 960,
                            use_angle_cls = true,
                            cls_thresh = 0.9f,
                            use_gpu = false,
                            enable_mkldnn = true,
                            cpu_math_library_num_threads = 4
                        };
                        var tempEngine = new PaddleOCREngine(config, par);
                        using (var bitmapRoi = new Bitmap(roiPath))
                        {
                            OCRResult result = tempEngine.DetectText(bitmapRoi);

                            if (result != null && result.Text != null && result.TextBlocks != null)
                            {
                                // 1. 收集每一行的文本和box
                                var lines = new List<(string text, List<OCRPoint> box)>();
                                for (int i = 0; i < result.TextBlocks.Count && i < result.TextBlocks.Count; i++)
                                {
                                    lines.Add((result.TextBlocks[i].Text, result.TextBlocks[i].BoxPoints));
                                }

                                int imgWidth = roi.Width; // 当前识别区域的宽度

                                // 2. 按Y中心点排序
                                lines = lines.OrderBy(l => l.box.Average(p => p.Y)).ToList();

                                // 3. 分组为同一气泡（简单按Y中心点聚类）
                                float yThreshold = 30; // 行间最大距离
                                var bubbles = new List<List<(string text, List<OCRPoint> box)>>();

                                foreach (var line in lines)
                                {
                                    double centerY = line.box.Average(p => p.Y);
                                    var found = bubbles.FirstOrDefault(b => Math.Abs(b.Last().box.Average(p => p.Y) - centerY) < yThreshold);
                                    if (found != null)
                                        found.Add(line);
                                    else
                                        bubbles.Add(new List<(string, List<OCRPoint>)> { line });
                                }

                                // 4. 合并相近的气泡
                                var mergedBubbles = new List<List<(string text, List<OCRPoint> box)>>();
                                if (bubbles.Any())
                                {
                                    var currentBubble = bubbles[0];
                                    for (int i = 1; i < bubbles.Count - 1; i++)
                                    {
                                        var nextBubble = bubbles[i];
                                        var currentBottom = currentBubble.SelectMany(l => l.box).Max(p => p.Y);
                                        var nextTop = nextBubble.SelectMany(l => l.box).Min(p => p.Y);

                                        // 如果两个气泡重叠或间距小于5像素，且属于同一说话者，则合并
                                        if (nextTop - currentBottom < 10)
                                        {
                                            currentBubble.AddRange(nextBubble);
                                        }
                                        else
                                        {
                                            mergedBubbles.Add(currentBubble);
                                            currentBubble = nextBubble;
                                        }
                                    }
                                    mergedBubbles.Add(currentBubble);
                                }

                                // 5. 输出每个气泡并发送到Coze
                                var allMessages = new List<string>();
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    int messageBoundary = int.Parse(txtMessageBoundary.Text);
                                    foreach (var bubble in mergedBubbles)
                                    {
                                        float minX = bubble.SelectMany(l => l.box).Min(p => p.X);
                                        string who = minX < messageBoundary ? "对方" : "自己";
                                        string text = string.Join("", bubble.Select(l => l.text));
                                        string message = $"[{who}说] {text}";
                                        Log.Text += message + "\n";
                                        allMessages.Add(message);
                                    }
                                    Log.ScrollToEnd();
                                });

                                // 6. 发送到Coze
                                try
                                {
                                    string cozeToken = await Dispatcher.InvokeAsync(() => txtCozeToken.Text);
                                    string cozeAppId = await Dispatcher.InvokeAsync(() => txtCozeAppId.Text);
                                    string cozeWorkflowId = await Dispatcher.InvokeAsync(() => txtCozeWorkflowId.Text);

                                    if (string.IsNullOrEmpty(cozeToken) || string.IsNullOrEmpty(cozeAppId) || string.IsNullOrEmpty(cozeWorkflowId))
                                    {
                                        throw new InvalidOperationException("Coze Token, AppId and WorkflowId must be provided");
                                    }

                                    // 创建CozeWorkflow实例
                                    var cozeWorkflow = new CozeWorkflow<Dta, DtaOutPut>(
                                        "https://api.coze.cn",  // 修改为正确的域名
                                        cozeToken,
                                        cozeWorkflowId,
                                        cozeAppId
                                    );

                                    // 将消息合并成一个字符串
                                    string combinedMessage = string.Join("\n", allMessages);

                                    Dta dta = new Dta();
                                    dta.input = combinedMessage;

                                    // 使用workflow处理消息
                                    var response = await cozeWorkflow.RunWorkflowAsync(dta);

                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        Log.Text += "等待响应...\n";
                                        Log.ScrollToEnd();
                                    });

                                    if (response.Code == 0 && response.Data != null)
                                    {
                                        DtaOutPut deserializeObject = JsonConvert.DeserializeObject<DtaOutPut>(response.Data);
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            Log.Text += $"Coze响应: {deserializeObject.output}\n";
                                            Log.ScrollToEnd();
                                        });
                                        tempEngine.Dispose();
                                        return deserializeObject.output;
                                    }
                                    else
                                    {
                                        tempEngine.Dispose();
                                        throw new Exception($"Coze API返回错误: {response.Msg}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        Log.Text += $"发送到Coze失败: {ex.Message}\n";
                                        Log.ScrollToEnd();
                                    });
                                    tempEngine.Dispose();
                                    return null;
                                }
                            }
                            else
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    Log.Text += $"在指定区域内未识别到文字 (区域: 左{leftOffset} 右{rightOffset} 上{topOffset} 下{bottomOffset})\n";
                                    Log.ScrollToEnd();
                                });
                                tempEngine.Dispose();
                                return null;
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Log.Text += $"OCR识别失败: {ex.Message}\n";
                    Log.Text += $"错误详情: {ex.ToString()}\n";
                    Log.ScrollToEnd();
                });
                return null;
            }
            return null;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    string json = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (settings != null)
                    {
                        // 暂时禁用TextChanged事件
                        txtMessageBoundary.TextChanged -= TxtMessageBoundary_TextChanged;
                        txtCozeToken.TextChanged -= TxtCozeToken_TextChanged;
                        txtCozeAppId.TextChanged -= TxtCozeAppId_TextChanged;
                        txtCozeWorkflowId.TextChanged -= TxtCozeWorkflowId_TextChanged;
                        txtLeftOffset.TextChanged -= TxtLeftOffset_TextChanged;
                        txtRightOffset.TextChanged -= TxtRightOffset_TextChanged;
                        txtTopOffset.TextChanged -= TxtTopOffset_TextChanged;
                        txtBottomOffset.TextChanged -= TxtBottomOffset_TextChanged;
                        txtSendClickYOffset.TextChanged -= txtSendClickYOffset_TextChanged;

                        selectedImagePath = settings.SelectedImagePath;
                        selectedImagePath1 = settings.SelectedImagePath1;
                        selectedImagePath2 = settings.SelectedImagePath2;
                        txtCozeToken.Text = settings.CozeToken;
                        txtCozeAppId.Text = settings.CozeAppId;
                        txtCozeWorkflowId.Text = settings.CozeWorkflowId;
                        txtMessageBoundary.Text = settings.MessageBoundary.ToString();
                        txtLeftOffset.Text = settings.LeftOffset.ToString();
                        txtRightOffset.Text = settings.RightOffset.ToString();
                        txtTopOffset.Text = settings.TopOffset.ToString();
                        txtBottomOffset.Text = settings.BottomOffset.ToString();
                        txtMatchThreshold.Text = settings.MatchThreshold ?? "0.85";
                        txtScreenshotLeftOffset.Text = settings.ScreenshotLeftOffset.ToString();
                        txtSendClickYOffset.Text = settings.SendClickYOffset.ToString();

                        // 重新启用TextChanged事件
                        txtMessageBoundary.TextChanged += TxtMessageBoundary_TextChanged;
                        txtCozeToken.TextChanged += TxtCozeToken_TextChanged;
                        txtCozeAppId.TextChanged += TxtCozeAppId_TextChanged;
                        txtCozeWorkflowId.TextChanged += TxtCozeWorkflowId_TextChanged;
                        txtLeftOffset.TextChanged += TxtLeftOffset_TextChanged;
                        txtRightOffset.TextChanged += TxtRightOffset_TextChanged;
                        txtTopOffset.TextChanged += TxtTopOffset_TextChanged;
                        txtBottomOffset.TextChanged += TxtBottomOffset_TextChanged;
                        txtSendClickYOffset.TextChanged += txtSendClickYOffset_TextChanged;

                        // Update button text for first image
                        if (!string.IsNullOrEmpty(selectedImagePath))
                        {
                            var template = btnSelectImage.Template;
                            if (template != null)
                            {
                                var imageNameText = template.FindName("ImageNameText", btnSelectImage) as System.Windows.Controls.TextBlock;
                                if (imageNameText != null)
                                {
                                    imageNameText.Text = System.IO.Path.GetFileName(selectedImagePath);
                                }
                            }
                        }

                        // Update button text for second image
                        if (!string.IsNullOrEmpty(selectedImagePath1))
                        {
                            var template = btnSelectImage1.Template;
                            if (template != null)
                            {
                                var imageNameText = template.FindName("ImageNameText", btnSelectImage1) as System.Windows.Controls.TextBlock;
                                if (imageNameText != null)
                                {
                                    imageNameText.Text = System.IO.Path.GetFileName(selectedImagePath1);
                                }
                            }
                        }

                        // Update button text for third image
                        if (!string.IsNullOrEmpty(selectedImagePath2))
                        {
                            var template = btnSelectImage2.Template;
                            if (template != null)
                            {
                                var imageNameText = template.FindName("ImageNameText", btnSelectImage2) as System.Windows.Controls.TextBlock;
                                if (imageNameText != null)
                                {
                                    imageNameText.Text = System.IO.Path.GetFileName(selectedImagePath2);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Text += $"加载设置失败: {ex.Message}\n";
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    SelectedImagePath = selectedImagePath,
                    SelectedImagePath1 = selectedImagePath1,
                    SelectedImagePath2 = selectedImagePath2,
                    CozeToken = txtCozeToken.Text,
                    CozeAppId = txtCozeAppId.Text,
                    CozeWorkflowId = txtCozeWorkflowId.Text,
                    MessageBoundary = int.Parse(string.IsNullOrEmpty(txtMessageBoundary.Text) ? "0" : txtMessageBoundary.Text),
                    LeftOffset = int.Parse(string.IsNullOrEmpty(txtLeftOffset.Text) ? "0" : txtLeftOffset.Text),
                    RightOffset = int.Parse(string.IsNullOrEmpty(txtRightOffset.Text) ? "0" : txtRightOffset.Text),
                    TopOffset = int.Parse(string.IsNullOrEmpty(txtTopOffset.Text) ? "0" : txtTopOffset.Text),
                    BottomOffset = int.Parse(string.IsNullOrEmpty(txtBottomOffset.Text) ? "0" : txtBottomOffset.Text),
                    MatchThreshold = string.IsNullOrEmpty(txtMatchThreshold.Text) ? "0.85" : txtMatchThreshold.Text,
                    ScreenshotLeftOffset = int.Parse(string.IsNullOrEmpty(txtScreenshotLeftOffset.Text) ? "0" : txtScreenshotLeftOffset.Text),
                    SendClickYOffset = int.Parse(string.IsNullOrEmpty(txtSendClickYOffset.Text) ? "-100" : txtSendClickYOffset.Text),
                };

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json);
            }
            catch (Exception ex)
            {
                Log.Text += $"保存设置失败: {ex.Message}\n";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 移除键盘钩子
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
            }
            SaveSettings();
            StopCapture();
            base.OnClosed(e);
        }

        private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
                Title = "选择图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath = openFileDialog.FileName;
                // 更新按钮中的图片名称
                var button = sender as Button;
                if (button != null)
                {
                    var template = button.Template;
                    var imageNameText = template.FindName("ImageNameText", button) as System.Windows.Controls.TextBlock;
                    if (imageNameText != null)
                    {
                        imageNameText.Text = System.IO.Path.GetFileName(selectedImagePath);
                    }
                }
                SaveSettings();
            }
        }

        private void BtnSelectImage1_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
                Title = "选择图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath1 = openFileDialog.FileName;
                // 更新按钮中的图片名称
                var button = sender as Button;
                if (button != null)
                {
                    var template = button.Template;
                    var imageNameText = template.FindName("ImageNameText", button) as System.Windows.Controls.TextBlock;
                    if (imageNameText != null)
                    {
                        imageNameText.Text = System.IO.Path.GetFileName(selectedImagePath1);
                    }
                }
                SaveSettings();
            }
        }

        private void BtnSelectImage2_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
                Title = "选择图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath2 = openFileDialog.FileName;
                // 更新按钮中的图片名称
                var button = sender as Button;
                if (button != null)
                {
                    var template = button.Template;
                    var imageNameText = template.FindName("ImageNameText", button) as System.Windows.Controls.TextBlock;
                    if (imageNameText != null)
                    {
                        imageNameText.Text = System.IO.Path.GetFileName(selectedImagePath2);
                    }
                }
                SaveSettings();
            }
        }

        private void TxtCozeToken_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
            InitializeCozeClient();
        }

        private void TxtCozeAppId_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
            InitializeCozeClient();
        }

        private void TxtCozeWorkflowId_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
            InitializeCozeClient();
        }

        private void TxtMessageBoundary_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtMessageBoundary.Text))
                return;

            if (int.TryParse(txtMessageBoundary.Text, out int value))
            {
                SaveSettings();
            }
        }

        private void TxtLeftOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtLeftOffset.Text))
                return;

            if (int.TryParse(txtLeftOffset.Text, out int value))
            {
                SaveSettings();
            }
        }

        private void TxtRightOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtRightOffset.Text))
                return;

            if (int.TryParse(txtRightOffset.Text, out int value))
            {
                SaveSettings();
            }
        }

        private void TxtTopOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtTopOffset.Text))
                return;

            if (int.TryParse(txtTopOffset.Text, out int value))
            {
                SaveSettings();
            }
        }

        private void TxtBottomOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtBottomOffset.Text))
                return;

            if (int.TryParse(txtBottomOffset.Text, out int value))
            {
                SaveSettings();
            }
        }

        private void txtSendClickYOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private async void LoadAdBar()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // TODO: 替换为你的广告接口地址
                    //var resp = await client.GetStringAsync("http://example.com/ad");
                    //var ad = JsonConvert.DeserializeObject<AdModel>(resp);

                    AdModel ad = new AdModel();
                    ad.content = "微信：shuaidekuerdoudiaole(点击复制)";
                    ad.url = "https://www.toutiao.com/?is_new_connect=0&is_new_user=0";

                    if (!string.IsNullOrEmpty(ad.content) && !string.IsNullOrEmpty(ad.url))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AdContent.Text = ad.content;
                            adUrl = ad.url;
                            AdBar.Visibility = Visibility.Visible;

                            // 跑马灯动画
                            AdContent.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                            adTextWidth = AdContent.DesiredSize.Width;
                            adCanvasWidth = AdCanvas.ActualWidth > 0 ? AdCanvas.ActualWidth : 400;
                            adCurrentLeft = adCanvasWidth;
                            Canvas.SetLeft(AdContent, adCurrentLeft);

                            if (adScrollTimer == null)
                            {
                                adScrollTimer = new DispatcherTimer();
                                adScrollTimer.Interval = TimeSpan.FromMilliseconds(10);
                                adScrollTimer.Tick += (s, e) =>
                                {
                                    adCurrentLeft -= 1;
                                    Canvas.SetLeft(AdContent, adCurrentLeft);
                                    if (adCurrentLeft < -adTextWidth)
                                    {
                                        adCurrentLeft = adCanvasWidth;
                                    }
                                };
                            }
                            adScrollTimer.Start();
                        });

                    }
                }
            }
            catch { /* 可忽略异常或写日志 */ }
        }

        private void AdBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(adUrl))
            {
                try
                {
                    //Process.Start(new ProcessStartInfo(adUrl) { UseShellExecute = true });
                    Clipboard.SetText("shuaidekuerdoudiaole");
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("微信号已复制");
                    });
                }
                catch { }
            }
        }

        private void TxtMatchThreshold_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private void BtnRefreshWindowList_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindowList();
            selectedWindowHandle = IntPtr.Zero;
            Log.Text += "窗口列表已刷新\n";
            Log.ScrollToEnd();
        }
        bool hasAdd = false;
        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            if (!hasAdd)
            {
                this.Height += 120;
                hasAdd = true;
            }

        }

        private void txtScreenshotLeftOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}