using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SeliwareAPI;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SynapseUI
{
    public partial class MainForm : Form
    {
        
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;



            // 移除标题栏


        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST)
            {
                // 获取鼠标位置
                Point pos = new Point(m.LParam.ToInt32());
                pos = this.PointToClient(pos);

                // 检测边缘区域（5像素的边界区域）
                if (pos.X <= 1)
                {
                    if (pos.Y <= 1)
                        m.Result = (IntPtr)HTTOPLEFT;
                    else if (pos.Y >= this.ClientSize.Height - 1)
                        m.Result = (IntPtr)HTBOTTOMLEFT;
                    else
                        m.Result = (IntPtr)HTLEFT;
                }
                else if (pos.X >= this.ClientSize.Width - 1)
                {
                    if (pos.Y <= 1)
                        m.Result = (IntPtr)HTTOPRIGHT;
                    else if (pos.Y >= this.ClientSize.Height - 1)
                        m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else
                        m.Result = (IntPtr)HTRIGHT;
                }
                else if (pos.Y <= 1)
                {
                    m.Result = (IntPtr)HTTOP;
                }
                else if (pos.Y >= this.ClientSize.Height - 1)
                {
                    m.Result = (IntPtr)HTBOTTOM;
                }
                else
                {
                    // 非边界区域可拖动
                    m.Result = (IntPtr)HTCAPTION;
                }
            }
        }

    
        private void Seliware_MessageReceived(object sender, MessageEventArgs e,string messageJson)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<Dictionary<string, object>>(messageJson);
                if (message.ContainsKey("type") && message["type"].ToString() == "console_message")
                {
                    SendMessageToUI(new
                    {
                        type = "console_message",
                        text = message["text"].ToString(),
                        messageType = message["messageType"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Lua message: {ex.Message}");
            }
        }
        private class LogHandler
        {
            private readonly MainForm _form;

            public LogHandler(MainForm form)
            {
                _form = form;
            }

            public void SetupLogService()
            {

            }
        }

        private void mf()
        {
            string luaCode = @"
local ws = WebSocket.connect('ws://localhost:8765/ui')
local logserv = game:GetService('LogService')
logserv.MessageOut:Connect(function(output, OutputType)
    local messageType = 'info'
    
    if OutputType == Enum.MessageType.MessageError then
        messageType = 'error'
    elseif OutputType == Enum.MessageType.MessageWarning then
        messageType = 'warning'
    end
    local jdata = game:GetService('HttpService'):JSONEncode({type = 'console_message',text = output,messageType = messageType})
    ws:Send(jdata)
end)
        ";
            string un = System.Environment.UserName;
            string pt = "C:\\Users\\" + un + "\\AppData\\Local\\seliware-autoexec\\AAA_console.lua";
            if (!File.Exists(pt))
            {
                File.WriteAllText(pt,luaCode);
                return;
            }
            File.Delete(pt);
            File.WriteAllText(pt,luaCode);
        }
        private WebView2 _webView;
        private string _scriptsFolder;
        private bool _isMaximized = false;
        private Point _originalLocation;
        private Size _originalSize;
        private WebSocketServer _webSocketServer;
        private const int WEBSOCKET_PORT = 8765;
        
        // 设置相关
        private bool _autoInjectEnabled = false;
        private Thread _autoInjectThread;
        private bool _isRunning = true;
        private string _settingsPath;
        private string _currentFolder = "/";
        private HashSet<int> _injectedProcessIds = new HashSet<int>();

        // 用于窗口拖动的 Win32 API
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        
        public static void MoveWindow(IntPtr handle)
        {
            ReleaseCapture();
            SendMessage(handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        public MainForm()
        {
            Seliware.Initialize();
            InitializeComponent();
            InitializeWebSocketServer();
            InitializeAsync();
            InitializeWindow();
            InitializeSeliware();
            LoadSettings();
            SendCurrentSettings();
            mf();
            this.FormBorderStyle = FormBorderStyle.None;
            // 启用双缓冲减少闪烁
            this.DoubleBuffered = true;
            // 允许调整大小
            this.ResizeRedraw = true;
        
        }

        private void InitializeWebSocketServer()
        {
            _webSocketServer = new WebSocketServer(WEBSOCKET_PORT);
            _webSocketServer.AddWebSocketService<UIWebSocket>("/ui", () => new UIWebSocket(this));
            _webSocketServer.Start();
            Debug.WriteLine($"WebSocket server started on port {WEBSOCKET_PORT}");
        }

        private void InitializeWindow()
        {
            _originalLocation = this.Location;
            _originalSize = this.Size;
        }
        

        private async void InitializeAsync()
        {
            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            this.Controls.Add(_webView);
            
            var env = await CoreWebView2Environment.CreateAsync();
            await _webView.EnsureCoreWebView2Async(env);
            
            _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            
            string htmlPath = Path.Combine(Application.StartupPath, "core", "mainsite.html");
            if (File.Exists(htmlPath))
            {
                string webSocketScript = $@"<script>
                    const WS_PORT = {WEBSOCKET_PORT};
                    const socket = new WebSocket(`ws://localhost:${{WS_PORT}}/ui`);
                    
                    socket.addEventListener('open', (event) => {{
                        console.log('WebSocket connected');
                        // 连接后立即请求scripts列表
                        window.postMessageToServer({{ type: 'get_scripts_list', path: '/' }});
                    }});
                    
                    socket.addEventListener('message', (event) => {{
                        const message = JSON.parse(event.data);
                        console.log('Received from server:', message);
                        handleWebViewMessage(message);
                    }});
                    
                    window.postMessageToServer = function(message) {{
                        console.log('Sending to server:', message);
                        socket.send(JSON.stringify(message));
                    }};
                </script>";
                
                string htmlContent = File.ReadAllText(htmlPath);
                htmlContent = htmlContent.Replace("</body>", $"{webSocketScript}</body>");
                _webView.CoreWebView2.NavigateToString(htmlContent);
            }
            else
            {
                MessageBox.Show("UI file not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SendMessageToUI(object message)
        {
            if (_webSocketServer != null && _webSocketServer.IsListening)
            {
                _webSocketServer.WebSocketServices["/ui"].Sessions.Broadcast(JsonConvert.SerializeObject(message));
            }
        }

        private void InitializeSeliware()
        {
            try
            {
                _scriptsFolder = Path.Combine(Application.StartupPath, "scripts");
                if (!Directory.Exists(_scriptsFolder))
                {
                    Directory.CreateDirectory(_scriptsFolder);
                }
                
                CreateSampleScripts();
                SendScriptsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateSampleScripts()
        {
            string[] sampleScripts = {
                "Aimbot.lua",
                "ESP.lua",
                "AutoFarm.lua",
                "Teleport.lua",
                "SpeedHack.lua",
                "PlayerFly.lua",
                "NoClip.lua",
                "AutoCollect.lua"
            };

            foreach (var script in sampleScripts)
            {
                string filePath = Path.Combine(_scriptsFolder, script);
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, $"-- {script}\n-- Auto-generated sample script\n\nprint('Running {script}')\n");
                }
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Debug.WriteLine($"WebView2 message: {e.WebMessageAsJson}");
        }

        public void HandleMessageFromUI(string messageJson)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<Dictionary<string, object>>(messageJson);
                string type = message["type"].ToString();

                this.Invoke((MethodInvoker)delegate
                {
                    switch (type)
                    {
                        case "console_message":
                            Seliware_MessageReceived(null, null, messageJson);
                            break;

                        case "window_action":
                            HandleWindowAction(message["action"].ToString());
                            break;

                        case "get_scripts_list":
                            if (message.ContainsKey("path"))
                            {
                                _currentFolder = message["path"].ToString();
                            }

                            SendScriptsList();
                            break;

                        case "execute":
                            string code = message.ContainsKey("code") ? message["code"].ToString() : "";
                            string tabId = message.ContainsKey("tabId") ? message["tabId"].ToString() : "";
                            ExecuteCode(code, tabId);
                            break;

                        case "open_file":
                            string openTabId = message.ContainsKey("tabId") ? message["tabId"].ToString() : "";
                            OpenFile(openTabId);
                            break;

                        case "save_file":
                            string saveCode = message.ContainsKey("code") ? message["code"].ToString() : "";
                            string saveTabId = message.ContainsKey("tabId") ? message["tabId"].ToString() : "";
                            SaveFile(saveCode, saveTabId);
                            break;

                        case "injectroblox":
                            InjectSeliware();
                            break;

                        case "load_file":
                            string loadPath = message.ContainsKey("path") ? message["path"].ToString() : "";
                            string loadTabId = message.ContainsKey("tabId") ? message["tabId"].ToString() : "";
                            LoadFile(loadPath, loadTabId);
                            break;

                        case "execute_file":
                            string executePath = message.ContainsKey("path") ? message["path"].ToString() : "";
                            ExecuteFile(executePath);
                            break;

                        case "delete_file":
                            string deletePath = message.ContainsKey("path") ? message["path"].ToString() : "";
                            DeleteFile(deletePath);
                            break;

                        case "drag_window":
                            if (!_isMaximized)
                            {
                                MoveWindow(this.Handle);
                            }

                            break;

                        case "set_topmost":
                            bool topmost = Convert.ToBoolean(message["value"]);
                            this.TopMost = topmost;
                            SaveSettings();
                            SendCurrentSettings();
                            break;

                        case "set_autoinject":
                            _autoInjectEnabled = Convert.ToBoolean(message["value"]);
                            SaveSettings();

                            if (_autoInjectEnabled)
                            {
                                StartAutoInjectLoop();
                            }
                            else
                            {
                                StopAutoInjectLoop();
                            }

                            SendCurrentSettings();
                            break;

                        case "get_settings":
                            SendCurrentSettings();
                            break;
                        case "update":
                            Updater();
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling message: {ex.Message}");
            }
        }

        private void Updater()
        {
            string upbat = @"@echo off
setlocal enabledelayedexpansion

set ""github_url=https://github.com/REKMS-cttub/SynUI/releases/latest/download/Release.zip""
set ""temp_dir=%TEMP%\SynUI_Download""
set ""zip_file=Release.zip""
set ""max_retries=3""
set ""retry_delay=5""

if not exist ""!temp_dir!"" mkdir ""!temp_dir!""

:download_retry
set /a attempt+=1
echo Downloading Release.zip (Attempt !attempt! of %max_retries%)...

powershell -Command """"""Invoke-WebRequest -Uri '!github_url!' -OutFile '!cd!\!zip_file!'""""""

if %errorlevel% neq 0 (
    echo ERROR: Download failed!
    if !attempt! lss %max_retries% (
        echo Retrying in %retry_delay% seconds...
        timeout /t %retry_delay% /nobreak >nul
        goto download_retry
    )
    echo ERROR: Maximum download attempts reached!
    pause
    exit /b 1
)

echo Extracting files...
powershell -Command """"""Expand-Archive -Path '!cd!\!zip_file!' -DestinationPath '!cd!' -Force"""""" >nul 2>&1

if %errorlevel% neq 0 (
    echo ERROR: Extraction failed!
    pause
    exit /b 1
)

echo Cleaning up...
rmdir /s /q ""!temp_dir!"" 2>nul

echo Operation completed successfully!
del ""!cd!\!zip_file!"" >nul
start """" synapse_x_v3.exe
pause

powershell -Command """"""Start-Sleep -Milliseconds 300; Remove-Item -LiteralPath '%0' -Force""""""";
            File.WriteAllText("GetSynUI.bat",upbat);
            Process process = new Process();

// 设置进程启动信息
            process.StartInfo.FileName = "GetSynUI.bat";
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName("GetSynUI.bat");
            process.StartInfo.CreateNoWindow = false; // 不创建新窗口
            process.StartInfo.UseShellExecute = false; // 使用操作系统外壳程序启动进程
            process.Start();
            Application.Exit();
        }
        private void HandleWindowAction(string action)
        {
            switch (action)
            {
                case "minimize":
                    this.WindowState = FormWindowState.Minimized;
                    break;
                    
                case "maximize":
                    ToggleMaximize();
                    break;
                    
                case "close":
                    this.Close();
                    break;
            }
        }

        private void ToggleMaximize()
        {
            if (_isMaximized)
            {
                this.Location = _originalLocation;
                this.Size = _originalSize;
                _isMaximized = false;
            }
            else
            {
                _originalLocation = this.Location;
                _originalSize = this.Size;
                this.Location = new Point(0, 0);
                this.Size = new Size(
                    Screen.PrimaryScreen.WorkingArea.Width,
                    Screen.PrimaryScreen.WorkingArea.Height
                );
                _isMaximized = true;
            }
        }

// 修改 SendScriptsList 方法
        private void SendScriptsList()
        {
            try
            {
                var files = new List<object>();
        
                // 添加上级目录选项
                if (_currentFolder != "/")
                {
                    string parentPath = _currentFolder.Substring(0, _currentFolder.LastIndexOf('/'));
                    if (string.IsNullOrEmpty(parentPath)) parentPath = "/";
            
                    files.Add(new {
                        name = ".. (Back)",
                        path = parentPath,
                        type = "folder-up"
                    });
                }
        
                // 构建完整路径 - 修复路径拼接
                string fullPath = Path.Combine(_scriptsFolder, _currentFolder.TrimStart('/').Replace("/", "\\"));
        
                // 确保目录存在
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
        
                // 添加文件夹
                var directories = Directory.GetDirectories(fullPath);
                foreach (var dir in directories)
                {
                    string dirName = Path.GetFileName(dir);
                    files.Add(new { 
                        name = dirName, 
                        path = $"{_currentFolder}/{dirName}",  // 使用当前路径拼接
                        type = "folder" 
                    });
                }
        
                // 添加文件
                var scriptFiles = Directory.GetFiles(fullPath, "*.lua");
                foreach (var file in scriptFiles)
                {
                    string fileName = Path.GetFileName(file);
                    files.Add(new { 
                        name = fileName, 
                        path = $"{_currentFolder}/{fileName}",  // 使用当前路径拼接
                        type = "file" 
                    });
                }
        
                var message = new { 
                    type = "file_list", 
                    files = files,
                    currentPath = _currentFolder
                };
        
                SendMessageToUI(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending file list: {ex.Message}");
            }
        }

        private void ExecuteCode(string code, string tabId)
        {
            try
            {
                string[] processNames = { "RobloxPlayerBeta", "Roblox", "RobloxGameClient" };
                bool processFound = false;
                
                foreach (var name in processNames)
                {
                    Process[] processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0)
                    {
                        processFound = true;
                        break;
                    }
                }
                
                if (!processFound)
                {
                    SendMessageToUI(new
                    {
                        type = "execute_result",
                        success = false,
                        reason = "not injected",
                        tabId = tabId
                    });
                    SendInjectionResult(false, "No Roblox process found");
                    return;
                }
                
                if (!Seliware.IsInjected())
                {
                    SendMessageToUI(new
                    {
                        type = "execute_result",
                        success = false,
                        reason = "Not injected",
                        tabId = tabId
                    });
                    return;
                }
                
                Debug.WriteLine($"Executing code in tab {tabId}:\n{code}");
                Seliware.Execute(code);
                SendMessageToUI(new
                {
                    type = "execute_result",
                    success = true,
                    tabId = tabId
                });
            }
            catch (Exception ex)
            {
                SendMessageToUI(new
                {
                    type = "execute_result",
                    success = false,
                    reason = ex.Message,
                    tabId = tabId
                });
            }
        }

        private void OpenFile(string tabId)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Lua Files (*.lua)|*.lua|All Files (*.*)|*.*";
                dialog.InitialDirectory = _scriptsFolder;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadFileToUI(dialog.FileName, tabId);
                }
            }
        }

        private void LoadFileToUI(string filePath, string tabId)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                string fileName = Path.GetFileName(filePath);
        
                var message = new
                {
                    type = "file_loaded",
                    name = fileName,
                    content = content,
                    path = filePath.Replace(_scriptsFolder, "").Replace("\\", "/").TrimStart('/'),
                    tabId = tabId
                };
        
                SendMessageToUI(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading file: {ex.Message}");
            }
        }

        private void SaveFile(string content, string tabId)
        {
            if (string.IsNullOrEmpty(content))
            {
                MessageBox.Show("Editor content is empty!", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Lua Files (*.lua)|*.lua|All Files (*.*)|*.*";
                dialog.InitialDirectory = _scriptsFolder;
                dialog.DefaultExt = "lua";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(dialog.FileName, content);
                        SendScriptsList();
                        
                        SendMessageToUI(new
                        {
                            type = "update_title",
                            title = Path.GetFileName(dialog.FileName),
                            path = dialog.FileName.Replace(_scriptsFolder, "").Replace("\\", "/").TrimStart('/'),
                            tabId = tabId
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void InjectSeliware()
        {               
            string[] processNames = { "RobloxPlayerBeta", "Roblox", "RobloxGameClient" };
            bool processFound = false;
                
            foreach (var name in processNames)
            {
                Process[] processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    processFound = true;
                    break;
                }
            }

            if (!processFound)
            {
                SendInjectionResult(true, "Failed to Inject : No process found");
                return;
            }
            try
            {
                
                Seliware.Inject();
                Seliware.Injected += delegate
                {
                    new LogHandler(this).SetupLogService();
                    SendInjectionResult(true, "Injected successfully");
                };
                
            }
            catch (Exception ex)
            {
                SendInjectionResult(false, ex.Message);
            }
        }
        

        
        private void OnInjectionFailed(object sender, string error)
        {
            SendInjectionResult(false, error);
        }
        
        private void SendInjectionResult(bool success, string message)
        {
            this.Invoke((MethodInvoker)delegate {
                var result = new {
                    type = "inject_result",
                    success = success,
                    error = message
                };
                
                SendMessageToUI(result);
            });
        }

        private void LoadFile(string filePath, string tabId)
        {
            string fullPath = Path.Combine(_scriptsFolder, filePath.TrimStart('/').Replace("/", "\\"));
            LoadFileToUI(fullPath, tabId);
        }

        private void ExecuteFile(string filePath)
        {
            try
            {
                if (!Seliware.IsInjected())
                {
                    MessageBox.Show("Please inject first!", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                string fullPath = Path.Combine(_scriptsFolder, filePath.TrimStart('/').Replace("/", "\\"));
                string content = File.ReadAllText(fullPath);
                Seliware.Execute(content);
                
                Debug.WriteLine($"Executing file: {fullPath}\n{content}");
                MessageBox.Show($"Executed file: {Path.GetFileName(fullPath)}", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteFile(string filePath)
        {
            try
            {
                string fullPath = Path.Combine(_scriptsFolder, filePath.TrimStart('/').Replace("/", "\\"));
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    SendScriptsList();
                    MessageBox.Show($"Deleted file: {Path.GetFileName(fullPath)}", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        public class Settings
        {
            public bool TopMost { get; set; }
            public bool AutoInject { get; set; }
        }
        
        private void LoadSettings()
        {
            _settingsPath = Path.Combine(Application.UserAppDataPath, "synapse_settings.json");
            if (File.Exists(_settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonConvert.DeserializeObject<Settings>(json);
                    
                    if (settings != null)
                    {
                        this.TopMost = settings.TopMost;
                        _autoInjectEnabled = settings.AutoInject;
                        if (_autoInjectEnabled)
                        {
                            StartAutoInjectLoop();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading settings: {ex.Message}");
                }
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                var settings = new Settings
                {
                    TopMost = this.TopMost,
                    AutoInject = _autoInjectEnabled
                };
                
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
                Debug.WriteLine("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        
        private void StartAutoInjectLoop()
        {
            if (_autoInjectThread != null && _autoInjectThread.IsAlive)
                return;
        
            _isRunning = true;
    
            _autoInjectThread = new Thread(() => 
            {
                while (_isRunning && _autoInjectEnabled)
                {
                    try
                    {
                        string[] processNames = { "RobloxPlayerBeta", "Roblox", "RobloxGameClient" };
                        List<Process> targetProcesses = new List<Process>();
                        
                        foreach (var name in processNames)
                        {
                            var processes = Process.GetProcessesByName(name);
                            foreach (var process in processes)
                            {
                                if (!_injectedProcessIds.Contains(process.Id))
                                {
                                    targetProcesses.Add(process);
                                }
                            }
                        }
                        
                        if (targetProcesses.Count > 0)
                        {
                            Debug.WriteLine("Auto-injecting into Roblox process...");
                            
                            this.Invoke((MethodInvoker)delegate {
                                try
                                {
                                    Seliware.Inject();
                                    foreach (var process in targetProcesses)
                                    {
                                        _injectedProcessIds.Add(process.Id);
                                    }

                                    Seliware.Injected += delegate
                                    {
                                        SendInjectionResult(true, "Auto-injected successfully");
                                    };
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Auto-inject failed: {ex.Message}");
                                    SendInjectionResult(false, ex.Message);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AutoInject error: {ex.Message}");
                    }
            
                    Thread.Sleep(5000);
                }
            });
    
            _autoInjectThread.IsBackground = true;
            _autoInjectThread.Start();
        }
        
        private void StopAutoInjectLoop()
        {
            _isRunning = false;
        }
        
        private void SendCurrentSettings()
        {
            var settings = new
            {
                type = "settings",
                topmost = this.TopMost,
                autoinject = _autoInjectEnabled
            };
            
            SendMessageToUI(settings);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _isRunning = false;
            SaveSettings();
            
            if (_webSocketServer != null && _webSocketServer.IsListening)
            {
                _webSocketServer.Stop();
            }
        }
    }

    public class UIWebSocket : WebSocketBehavior
    {
        private MainForm _mainForm;

        public UIWebSocket()
        {
        }

        public UIWebSocket(MainForm mainForm)
        {
            _mainForm = mainForm;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (_mainForm != null && e.IsText)
            {
                _mainForm.HandleMessageFromUI(e.Data);
            }
        }
    }
}