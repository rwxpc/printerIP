using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrinterIpTool
{
    // by 任我行电脑工作室
    public partial class MainForm : Form
    {
        private ServiceController _spooler;
        private readonly List<PrinterInfo> _printers = new List<PrinterInfo>();
        private bool _loading;

        public MainForm()
        {
            InitializeComponent();
            _spooler = new ServiceController("Spooler");
        }

        // 拦截系统级最大化/拉伸命令（含 Win + ↑ 快捷键、系统菜单"大小"）
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_SIZE = 0xF000;
            const int SC_MAXIMIZE = 0xF030;

            if (m.Msg == WM_SYSCOMMAND)
            {
                int cmd = m.WParam.ToInt32() & 0xFFF0;
                if (cmd == SC_MAXIMIZE || cmd == SC_SIZE) return;
            }
            base.WndProc(ref m);
        }

        #region 界面布局
        private void InitializeComponent()
        {
            this.Text = "网络打印机 IP 修改工具";
            this.Size = new Size(662, 470);
            // 禁止调整窗口大小：固定单线边框 + 禁用最大化 + 锁定最小/最大尺寸
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.MinimumSize = this.Size;
            this.MaximumSize = this.Size;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = LoadAppIcon();
            this.Load += MainForm_Load;

            // 服务区：运行状态 + 启动类型（自动=绿 / 手动=蓝 / 禁用=红）
            this.Label1 = new Label();
            this.Label1.Text = "Print Spooler：";
            this.Label1.Location = new Point(16, 18);
            this.Label1.AutoSize = true;
            this.Label1.Font = new Font(this.Font, FontStyle.Bold);
            this.StatusLight = new Label();
            this.StatusLight.Text = "未检测";
            this.StatusLight.Location = new Point(122, 18);
            this.StatusLight.AutoSize = true;
            this.StatusLight.Font = new Font(this.Font, FontStyle.Bold);
            this.lblStartType = new Label();
            this.lblStartType.Text = "启动类型：未检测";
            this.lblStartType.Location = new Point(190, 18);
            this.lblStartType.AutoSize = true;
            this.lblStartType.Font = new Font(this.Font, FontStyle.Bold);
            this.lblStartType.ForeColor = Color.DimGray;
            this.btnCheckSpooler = new Button();
            this.btnCheckSpooler.Text = "检查并启动服务";
            this.btnCheckSpooler.Location = new Point(452, 14);
            this.btnCheckSpooler.Size = new Size(150, 30);
            this.btnCheckSpooler.Click += BtnCheckSpooler_Click;

            // 列表区
            this.Label2 = new Label();
            this.Label2.Text = "本地打印机列表";
            this.Label2.Location = new Point(16, 58);
            this.Label2.AutoSize = true;
            this.Label2.Font = new Font(this.Font, FontStyle.Bold);
            this.btnRefresh = new Button();
            this.btnRefresh.Text = "刷新列表";
            this.btnRefresh.Location = new Point(128, 54);
            this.btnRefresh.Size = new Size(84, 26);
            this.btnRefresh.Click += BtnRefresh_Click;
            this.btnInstall = new Button();
            this.btnInstall.Text = "安装未列出打印机…";
            this.btnInstall.Location = new Point(218, 54);
            this.btnInstall.Size = new Size(130, 26);
            this.btnInstall.Click += BtnInstall_Click;
            this.btnAbout = new Button();
            this.btnAbout.Text = "关于";
            this.btnAbout.Location = new Point(354, 54);
            this.btnAbout.Size = new Size(56, 26);
            this.btnAbout.Click += BtnAbout_Click;
            this.chkShowAll = new CheckBox();
            this.chkShowAll.Text = "显示全部（含USB/虚拟）";
            this.chkShowAll.Location = new Point(416, 58);
            this.chkShowAll.Size = new Size(160, 22);
            this.chkShowAll.CheckedChanged += (s, e) => LoadPrinters();

            this.grid = new DataGridView();
            this.grid.Location = new Point(16, 86);
            this.grid.Size = new Size(607, 172);
            this.grid.ReadOnly = true;
            this.grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.grid.MultiSelect = false;
            this.grid.AllowUserToAddRows = false;
            this.grid.BackgroundColor = Color.White;
            this.grid.RowHeadersWidth = 24;
            this.grid.Columns.Add("colName", "打印机名称");
            this.grid.Columns.Add("colPort", "端口");
            this.grid.Columns.Add("colIp", "当前 IP");
            this.grid.Columns.Add("colType", "类型");
            this.grid.Columns.Add("colDefault", "默认");
            this.grid.Columns["colName"].Width = 232;
            this.grid.Columns["colPort"].Width = 105;
            this.grid.Columns["colIp"].Width = 90;
            this.grid.Columns["colType"].Width = 90;
            this.grid.Columns["colDefault"].Width = 50;
            this.grid.SelectionChanged += Grid_SelectionChanged;

            // IP 修改区：标签与输入框拉开足够距离，避免长标签被截断
            this.Label3 = new Label();
            this.Label3.Text = "新 IP 地址：";
            this.Label3.Location = new Point(16, 270);
            this.Label3.AutoSize = true;
            this.Label3.Font = new Font(this.Font, FontStyle.Bold);
            this.txtNewIp = new TextBox();
            this.txtNewIp.Location = new Point(240, 266);
            this.txtNewIp.Size = new Size(130, 28);
            this.txtNewIp.Font = new Font("Segoe UI", 11F);
            this.txtNewIp.MaxLength = 15;
            this.btnApplyIp = new Button();
            this.btnApplyIp.Text = "保存修改";
            this.btnApplyIp.Location = new Point(380, 264);
            this.btnApplyIp.Size = new Size(110, 32);
            this.btnApplyIp.Enabled = false;
            this.btnApplyIp.Click += BtnSave_Click;
            this.btnDefault = new Button();
            this.btnDefault.Text = "设为默认打印机";
            this.btnDefault.Location = new Point(498, 264);
            this.btnDefault.Size = new Size(124, 32);
            this.btnDefault.Enabled = false;
            this.btnDefault.Click += BtnDefault_Click;

            // 日志区：只保留最新 4 条，窗口可以更紧凑
            this.Label4 = new Label();
            this.Label4.Text = "操作日志（最新 4 条）：";
            this.Label4.Location = new Point(16, 308);
            this.Label4.AutoSize = true;
            this.Label4.Font = new Font(this.Font, FontStyle.Bold);
            this.txtLog = new TextBox();
            this.txtLog.Location = new Point(16, 328);
            this.txtLog.Size = new Size(607, 82);
            this.txtLog.Multiline = true;
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = ScrollBars.Vertical;
            this.txtLog.WordWrap = true;
            this.txtLog.BackColor = Color.WhiteSmoke;
            this.txtLog.Font = new Font("Segoe UI", 9F);

            this.Controls.AddRange(new Control[] {
                this.Label1, this.StatusLight, this.lblStartType, this.btnCheckSpooler,
                this.Label2, this.btnRefresh, this.btnInstall, this.btnAbout, this.chkShowAll,
                this.grid,
                this.Label3, this.txtNewIp, this.btnApplyIp, this.btnDefault,
                this.Label4, this.txtLog });
        }

        private Label Label1, Label2, Label3, Label4, StatusLight, lblStartType;
        private Button btnCheckSpooler, btnRefresh, btnInstall, btnAbout, btnApplyIp, btnDefault;
        private CheckBox chkShowAll;
        private DataGridView grid;
        private TextBox txtNewIp, txtLog;
        private PrinterInfo _selected;

        private Icon LoadAppIcon()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(p)) return new Icon(p);
            }
            catch { }
            return SystemIcons.Application;
        }
        #endregion

        #region 事件
        private void MainForm_Load(object s, EventArgs e)
        {
            ShowStartupNotice();
            Log("程序启动，正在检查 Print Spooler …");
            Log(TokenPrivilege.IsElevated()
                ? "✓ 权限检查：当前处于已提升的管理员会话。"
                : "⚠ 权限检查：当前未获得管理员权限，改 IP 很可能失败（请右键以管理员身份运行）。");

            // 先刷新一次，显示系统里真实的原始启动类型（这时还没做任何改动）
            RefreshServiceStatus();
            Log("检测到 Print Spooler 启动类型：" + StartTypeText() + "，当前状态：" + StatusLight.Text);

            EnsureSpooler(out string msg);
            RefreshServiceStatus();   // 修正后再刷新（通常会变为「自动」）
            if (msg.Length > 0) Log("⚠ " + msg);
            else Log("✓ 就绪：启动类型 " + StartTypeText() + "，状态 " + StatusLight.Text + "。");
            LoadPrinters();
        }

        /// <summary>启动时的提示弹窗：点「确认」关闭后进入主界面</summary>
        private void ShowStartupNotice()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "提示";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.Size = new Size(520, 200);
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.Icon = this.Icon;
                dlg.BackColor = Color.White;

                var pic = new PictureBox();
                pic.Image = SystemIcons.Information.ToBitmap();
                pic.Location = new Point(24, 30);
                pic.Size = new Size(32, 32);
                pic.SizeMode = PictureBoxSizeMode.StretchImage;

                var lab = new Label();
                lab.Text = "提示：请根据本办公室的打印机型号及 ip 进行修改。";
                lab.Location = new Point(74, 30);
                lab.Size = new Size(410, 60);
                lab.Font = new Font("微软雅黑", 11F, FontStyle.Regular);
                lab.ForeColor = Color.FromArgb(30, 30, 30);
                lab.TextAlign = ContentAlignment.TopLeft;

                var btn = new Button();
                btn.Text = "确认";
                btn.Size = new Size(110, 34);
                btn.Location = new Point(200, 110);
                btn.Font = new Font("微软雅黑", 10F, FontStyle.Regular);
                btn.Click += (s, e) => dlg.Close();

                dlg.Controls.AddRange(new Control[] { pic, lab, btn });
                dlg.AcceptButton = btn;
                dlg.ShowDialog(this);
            }
        }

        private void BtnCheckSpooler_Click(object s, EventArgs e)
        {
            Log("检查 Print Spooler …");
            EnsureSpooler(out string msg);
            RefreshServiceStatus();
            if (msg.Length > 0) Log("⚠ " + msg); else Log("✓ Print Spooler 已就绪。");
            LoadPrinters();
        }

        private void BtnRefresh_Click(object s, EventArgs e)
        {
            LoadPrinters();
        }

        private void BtnInstall_Click(object s, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "安装程序(*.exe)|*.exe|驱动包(*.zip)|*.zip|信息文件(*.inf)|*.inf|全部文件(*.*)|*.*";
                ofd.Title = "选择打印机安装程序 / 驱动包";
                if (ofd.ShowDialog() != DialogResult.OK) return;
                Installer.InstallDriver(ofd.FileName, out string msg);
                Log(msg);
                LoadPrinters();
            }
        }

        private void BtnAbout_Click(object s, EventArgs e)
        {
            MessageBox.Show(this,
                "网络打印机 IP 修改工具\n版本：" + Application.ProductVersion + "\n\n" +
                "功能：\n · 检查并启动/设自动 Print Spooler\n · 列出本地打印机（默认仅显示 TCP/IP 网络打印机）\n · 修改并保存端口 IP\n · 启动未列出打印机的安装程序\n\n" +
                "版权：by 任我行电脑工作室\n" +
                "适用 Windows 7 SP1 ~ Windows 11",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Grid_SelectionChanged(object s, EventArgs e)
        {
            if (_loading) return;
            if (grid.CurrentRow != null && grid.CurrentRow.Index < _printers.Count)
                _selected = _printers[grid.CurrentRow.Index];
            else _selected = null;
            UpdateSaveState();
        }

        private void BtnSave_Click(object s, EventArgs e)
        {
            if (_selected == null) return;
            if (!_selected.IsNetwork)
            {
                Log("⚠ 该打印机不是 TCP/IP 网络打印机，无 IP 可修改。");
                return;
            }
            string newIp = txtNewIp.Text.Trim();
            if (!IsValidIp(newIp)) { Log("⚠ 输入了非法 IPv4：" + newIp); return; }
            var r = MessageBox.Show(this,
                "将 \"" + _selected.Name + "\" 的 IP 由\n    " + _selected.Ip + "\n改为\n    " + newIp + "\n确认保存？",
                "确认修改", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (r != DialogResult.OK) return;

            string printerName = _selected.Name;
            string oldPort = _selected.PortName;

            // 保存前：若 Print Spooler 未运行，先设为「自动」并启动它（改端口必须依赖打印服务）
            EnsureSpooler(out string smsg);
            RefreshServiceStatus();
            if (smsg.Length > 0) Log("⚠ " + smsg);

            IpChanger.ChangePrinterIp(printerName, oldPort, newIp, out string via, out string err);
            if (err.Length > 0)
            {
                Log("⚠ " + err);
                var q = MessageBox.Show(this,
                    "无法在保持原端口名（" + oldPort + "）的前提下修改 IP。\n\n" +
                    "是否改为新建一个指向 " + newIp + " 的端口并切换？\n" +
                    "（选“否”将放弃本次修改，端口名保持原样不变）",
                    "是否换用新端口？", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (q != DialogResult.Yes)
                {
                    Log("已取消修改：端口名保持 " + oldPort + "，IP 未变更。");
                    return;
                }
                IpChanger.ForceNewPortAndSwitch(printerName, oldPort, newIp, out via, out err);
                if (err.Length > 0) { Log("⚠ " + err); return; }
            }

            Log("✓ " + via + "，新 IP：" + newIp + "（spooler 实时生效，无需重启服务）。");
            LoadPrinters(printerName);

            // 最终复核：从打印服务读回该打印机端口的真实地址
            string actual = "(未知)";
            if (_selected != null)
            {
                var ports = WmiPorts.GetAll();
                var pi = WmiPorts.FindByName(ports, _selected.PortName);
                if (pi != null) actual = pi.HostAddress;
            }
            Log(actual.Equals(newIp, StringComparison.OrdinalIgnoreCase)
                ? "✓ 复核通过：端口实际地址 = " + actual + "；端口名保持 " + oldPort
                : "⚠ 复核发现端口实际地址为 " + actual + "，与目标 " + newIp + " 不一致，请在系统端口设置中再确认一次。");
        }
        #endregion

        #region 业务
        /// <summary>读取 Print Spooler 的启动类型（自动 / 手动 / 禁用）</summary>
        private string StartTypeText()
        {
            try
            {
                _spooler.Refresh();
                switch (_spooler.StartType)
                {
                    case ServiceStartMode.Automatic: return "自动";
                    case ServiceStartMode.Manual: return "手动";
                    case ServiceStartMode.Disabled: return "禁用";
                    default: return _spooler.StartType.ToString();
                }
            }
            catch { return "未知"; }
        }

        private void RefreshServiceStatus()
        {
            // 运行状态
            try
            {
                _spooler.Refresh();
                if (_spooler.Status == ServiceControllerStatus.Running)
                {
                    StatusLight.Text = "运行中";
                    StatusLight.ForeColor = Color.Green;
                }
                else
                {
                    StatusLight.Text = "已停止";
                    StatusLight.ForeColor = Color.Red;
                }
            }
            catch
            {
                StatusLight.Text = "无法读取";
                StatusLight.ForeColor = Color.OrangeRed;
            }

            // 启动类型：自动（绿）/ 手动（蓝）/ 禁用（红）
            try
            {
                _spooler.Refresh();
                switch (_spooler.StartType)
                {
                    case ServiceStartMode.Automatic:
                        lblStartType.Text = "启动类型：自动";
                        lblStartType.ForeColor = Color.Green;
                        break;
                    case ServiceStartMode.Manual:
                        lblStartType.Text = "启动类型：手动";
                        lblStartType.ForeColor = Color.Blue;
                        break;
                    case ServiceStartMode.Disabled:
                        lblStartType.Text = "启动类型：禁用";
                        lblStartType.ForeColor = Color.Red;
                        break;
                    default:   // Boot / System：系统引导即启动，按「自动」口径显示
                        lblStartType.Text = "启动类型：" + _spooler.StartType;
                        lblStartType.ForeColor = Color.Green;
                        break;
                }
            }
            catch
            {
                lblStartType.Text = "启动类型：无法读取";
                lblStartType.ForeColor = Color.OrangeRed;
            }
        }

        private void EnsureSpooler(out string msg)
        {
            msg = "";
            try
            {
                _spooler.Refresh();
                if (_spooler.StartType != ServiceStartMode.Automatic)
                {
                    ServiceHelper.SetStartupAutomatic();
                    Log("已将 Print Spooler 启动类型设为 自动。");
                }
                if (_spooler.Status != ServiceControllerStatus.Running)
                {
                    _spooler.Start();
                    _spooler.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    Log("✓ 已启动 Print Spooler。");
                }
            }
            catch (Exception ex)
            {
                msg = "操作服务失败：" + ex.Message;
            }
        }

        private void LoadPrinters(string selectName = null)
        {
            _loading = true;
            try
            {
                bool showAll = chkShowAll != null && chkShowAll.Checked;
                var all = Printers.Scan();
                _printers.Clear();
                foreach (var p in all)
                    if (p.IsNetwork || showAll) _printers.Add(p);

                grid.Rows.Clear();
                string defName = DefaultPrinter.Get();
                foreach (var p in _printers)
                    grid.Rows.Add(p.Name, p.PortName,
                        string.IsNullOrEmpty(p.Ip) ? "-" : p.Ip,
                        p.IsNetwork ? "网络" : "本地/不可改",
                        (!string.IsNullOrEmpty(defName) && defName.Equals(p.Name, StringComparison.OrdinalIgnoreCase)) ? "★" : "");

                int netCount = 0;
                foreach (var p in all) if (p.IsNetwork) netCount++;
                Log(string.Format("扫描完成：本机共 {0} 台打印机，网络打印机 {1} 台{2}。",
                    all.Count, netCount,
                    showAll ? "（显示全部）" : "（仅显示网络；勾选复选框看全部）"));

                if (!string.IsNullOrEmpty(selectName))
                {
                    for (int i = 0; i < _printers.Count; i++)
                    {
                        if (_printers[i].Name == selectName)
                        {
                            grid.ClearSelection();
                            grid.Rows[i].Selected = true;
                            grid.CurrentCell = grid.Rows[i].Cells[0];
                            _selected = _printers[i];
                            break;
                        }
                    }
                }
                else if (_printers.Count > 0)
                {
                    // 默认选中第一台，让「新 IP」输入框自动填入它的当前 IP
                    grid.ClearSelection();
                    grid.Rows[0].Selected = true;
                    grid.CurrentCell = grid.Rows[0].Cells[0];
                    _selected = _printers[0];
                }
                else
                {
                    _selected = null;
                }
            }
            finally
            {
                _loading = false;
            }
            UpdateSaveState();
        }

        private void BtnDefault_Click(object s, EventArgs e)
        {
            if (_selected == null) { Log("⚠ 请先在列表中选择一台打印机。"); return; }

            string err;
            bool ok = DefaultPrinter.Set(_selected.Name, out err);
            if (!ok) { Log("⚠ 设置默认打印机失败：" + err); return; }

            Log("✓ 已将「" + _selected.Name + "」设为默认打印机。");
            LoadPrinters(_selected.Name);
        }

        private void UpdateSaveState()
        {
            bool ok = _selected != null && _selected.IsNetwork;
            btnApplyIp.Enabled = ok;
            btnDefault.Enabled = _selected != null;   // 任何打印机都可设为默认
            if (_selected != null)
            {
                // 每次切换选中都把原 IP 填入输入框，用户在此基础上改
                if (_selected.IsNetwork)
                {
                    this.Label3.Text = "新 IP（原 " + _selected.Ip + "）：";
                    txtNewIp.Text = _selected.Ip;
                }
                else
                {
                    this.Label3.Text = "新 IP 地址：";
                    txtNewIp.Text = "";
                }
            }
            else
            {
                this.Label3.Text = "新 IP 地址：";
                txtNewIp.Text = "";
            }
        }
        #endregion

        #region 工具
        // 操作日志只保留最新 4 条，界面更紧凑
        private const int MaxLogLines = 4;
        private readonly Queue<string> _logLines = new Queue<string>(MaxLogLines);

        private void Log(string t)
        {
            _logLines.Enqueue(DateTime.Now.ToString("HH:mm:ss") + "  " + t);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();

            txtLog.Text = string.Join(Environment.NewLine, _logLines.ToArray());
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        static bool IsValidIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            var parts = ip.Split('.');
            if (parts.Length != 4) return false;
            foreach (var p in parts)
            {
                int n;
                if (!int.TryParse(p, out n) || n < 0 || n > 255) return false;
            }
            return true;
        }
        #endregion
    }

    public class PrinterInfo
    {
        public string Name;
        public string PortName;
        public string Ip;
        public bool IsNetwork;
    }

    internal static class Reg
    {
        public const string PrintersKey = @"SYSTEM\CurrentControlSet\Control\Print\Printers";

        // 端口子键在不同 Windows 版本叫法不同：Win10/11 为 Ports，老版本为 Port List
        public static RegistryKey OpenPortsKey(bool writable = false)
        {
            var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var k = b.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Print\Monitors\Standard TCP/IP Port\Ports", writable);
            if (k == null)
                k = b.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Print\Monitors\Standard TCP/IP Port\Port List", writable);
            return k;
        }

        public static RegistryKey Open(string path, bool writable = false)
        {
            var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            return b.OpenSubKey(path, writable);
        }
    }

    /// <summary>标准 TCP/IP 端口的实时信息（来自打印服务）</summary>
    public class TcpPortInfo
    {
        public string Name;
        public string HostAddress;
        public uint Protocol;    // 1=RAW 2=LPR
        public uint PortNumber;
        public string Queue;     // LPR 队列名
        public bool SNMPEnabled;
        public string SNMPCommunity;
    }

    /// <summary>
    /// WMI Win32_TCPIPPrinterPort 是端口配置的权威实时数据源（spooler 直接提供），
    /// 且 Put() 修改 HostAddress 时保留协议/队列等其余配置 —— 对 LPR 端口也安全。
    /// </summary>
    internal static class WmiPorts
    {
        public static List<TcpPortInfo> GetAll()
        {
            var list = new List<TcpPortInfo>();
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_TCPIPPrinterPort"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        list.Add(new TcpPortInfo
                        {
                            Name = o["Name"] as string,
                            HostAddress = o["HostAddress"] as string,
                            Protocol = ToUint(o["Protocol"], 1),
                            PortNumber = ToUint(o["PortNumber"], 9100),
                            Queue = (o["Queue"] as string) ?? "",
                            SNMPEnabled = (o["SNMPEnabled"] as bool?) ?? false,
                            SNMPCommunity = (o["SNMPCommunity"] as string) ?? "public"
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        private static uint ToUint(object v, uint dflt)
        {
            try { return Convert.ToUInt32(v); } catch { return dflt; }
        }

        public static TcpPortInfo FindByAddress(List<TcpPortInfo> all, string ip)
        {
            foreach (var p in all)
                if (p.HostAddress != null && p.HostAddress.Trim().Equals(ip, StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }

        public static TcpPortInfo FindByName(List<TcpPortInfo> all, string name)
        {
            foreach (var p in all)
                if (p.Name != null && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }

        /// <summary>新建端口（继承模板端口的协议/队列，如 Brother 的 LPR+BINARY_P1）。
        /// 注意：HostAddress 建好后 WMI 里是只读的，改已有端口地址必须走 XcvPort.Configure。</summary>
        public static bool Create(string name, string ip, TcpPortInfo template, out string err)
        {
            err = "";
            try
            {
                using (var cls = new System.Management.ManagementClass("Win32_TCPIPPrinterPort"))
                using (var o = cls.CreateInstance())
                {
                    o["Name"] = name;
                    o["HostAddress"] = ip;
                    if (template != null && template.Protocol == 2)   // LPR
                    {
                        o["Protocol"] = 2u;
                        o["PortNumber"] = template.PortNumber == 0 ? 515u : template.PortNumber;
                        o["Queue"] = string.IsNullOrEmpty(template.Queue) ? "BINARY_P1" : template.Queue;
                        o["SNMPEnabled"] = template.SNMPEnabled;
                        if (!string.IsNullOrEmpty(template.SNMPCommunity))
                            o["SNMPCommunity"] = template.SNMPCommunity;
                    }
                    else                                              // RAW
                    {
                        o["Protocol"] = 1u;
                        o["PortNumber"] = 9100u;
                        o["Queue"] = "";
                        o["SNMPEnabled"] = false;
                    }
                    o.Put();
                }
                return true;
            }
            catch (Exception ex)
            {
                err = "WMI 建端口失败：" + ex.Message;
                return false;
            }
        }
    }

    public static class Printers
    {
        public static List<PrinterInfo> Scan()
        {
            var list = new List<PrinterInfo>();
            using (var root = Reg.Open(Reg.PrintersKey))
            {
                if (root == null) return list;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using (var k = root.OpenSubKey(sub))
                    {
                        if (k == null) continue;
                        var pv = k.GetValue("Port")?.ToString() ?? "";
                        string portName = "", ip = "";
                        foreach (var one in pv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var cand = one.Trim();
                            var found = ResolveIp(cand);
                            if (!string.IsNullOrEmpty(found))
                            {
                                portName = cand;
                                ip = found;
                                break;
                            }
                            if (string.IsNullOrEmpty(portName)) portName = cand;
                        }
                        list.Add(new PrinterInfo
                        {
                            Name = sub,
                            PortName = portName,
                            Ip = ip,
                            IsNetwork = !string.IsNullOrEmpty(ip)
                        });
                    }
                }
            }
            return list;
        }

        // 端口名与实际地址可能不一致（历史遗留/手工改过）：
        // 1) 优先读 WMI（spooler 实时数据）的 HostAddress；
        // 2) 再读注册表端口子键（Ports / Port List 自动兼容）；
        // 3) 都没有才退回「端口名本身含 IP」。
        public static string ResolveIp(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return "";

            var all = WmiPorts.GetAll();
            var w = WmiPorts.FindByName(all, portName);
            if (w != null && IsValidIpStr(w.HostAddress)) return w.HostAddress.Trim();

            using (var pl = Reg.OpenPortsKey())
            {
                if (pl != null)
                using (var pk = pl.OpenSubKey(portName))
                {
                    if (pk != null)
                    {
                        foreach (var vn in new[] { "HostName", "IPAddress", "PName" })
                        {
                            var v = pk.GetValue(vn)?.ToString();
                            if (string.IsNullOrEmpty(v)) continue;
                            var m = Regex.Match(v.Trim(), @"^(\d{1,3}\.){3}\d{1,3}$");
                            if (m.Success) return m.Value;
                        }
                    }
                }
            }

            var m2 = Regex.Match(portName, @"(\d{1,3}\.){3}\d{1,3}");
            return m2.Success ? m2.Value : "";
        }

        private static bool IsValidIpStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var parts = s.Trim().Split('.');
            if (parts.Length != 4) return false;
            foreach (var p in parts)
            {
                int n;
                if (!int.TryParse(p, out n) || n < 0 || n > 255) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// XcvDataW 的 AddPort 要求调用者「启用」SeLoadDriverPrivilege
    /// （管理员令牌里该特权默认存在但是禁用状态，必须显式打开，否则返回 status=5 拒绝访问）。
    /// </summary>
    internal static class TokenPrivilege
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr h, uint acc, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string host, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll,
            ref TOKEN_PRIVILEGES tp, uint len, IntPtr prev, IntPtr retLen);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr h);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr buf, int len, out int ret);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attr; }
        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint Count; public LUID_AND_ATTRIBUTES Priv; }

        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        public static void EnableSeLoadDriver()
        {
            IntPtr token;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                    TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out token)) return;
            try
            {
                LUID luid;
                if (!LookupPrivilegeValue(null, "SeLoadDriverPrivilege", out luid)) return;
                var tp = new TOKEN_PRIVILEGES
                {
                    Count = 1,
                    Priv = new LUID_AND_ATTRIBUTES { Luid = luid, Attr = SE_PRIVILEGE_ENABLED }
                };
                AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { CloseHandle(token); }
        }

        /// <summary>当前进程是否处于已提升的管理员会话</summary>
        public static bool IsElevated()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// 兜底层：用系统自带的 PowerShell PrintManagement 指令建端口/换端口
    /// （XcvDataW 与注册表都失败时启用，Win8/Win10/Win11 及装了 RSAT 的 Win7 可用）
    /// </summary>
    internal static class PsHelper
    {
        public static bool Run(string script, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo("powershell.exe")
                {
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                                script.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false,          // 直接继承本进程已提升的令牌，不再弹 UAC
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
                    p.WaitForExit(60000);
                    output = (o ?? "").Trim();
                    if (string.IsNullOrEmpty(output)) output = (e ?? "").Trim();
                    return p.ExitCode == 0 && string.IsNullOrEmpty(output);
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }

        private static string Q(string s) { return "'" + (s ?? "").Replace("'", "''") + "'"; }

        public static bool AddPort(string portName, string hostAddress, out string output)
        {
            return Run("$ErrorActionPreference='Stop'; " +
                       "if (-not (Get-PrinterPort -Name " + Q(portName) + " -ErrorAction SilentlyContinue)) " +
                       "{ Add-PrinterPort -Name " + Q(portName) + " -PrinterHostAddress " + Q(hostAddress) + " }",
                       out output);
        }

        public static bool SetPrinterPort(string printerName, string portName, out string output)
        {
            return Run("$ErrorActionPreference='Stop'; Set-Printer -Name " + Q(printerName) +
                       " -PortName " + Q(portName), out output);
        }
    }

    /// <summary>默认打印机的读取与设置（WMI Win32_Printer 为主，winspool API 兜底）</summary>
    public static class DefaultPrinter
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string pszPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);

        public static string Get()
        {
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Printer WHERE Default = True"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                        return o["Name"] as string ?? "";
                }
            }
            catch { }
            return "";
        }

        public static bool Set(string printerName, out string err)
        {
            err = "";
            // 1) WMI：调 Win32_Printer 的 SetDefaultPrinter 方法
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_Printer"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        var name = o["Name"] as string;
                        if (name != printerName) continue;
                        var ret = o.InvokeMethod("SetDefaultPrinter", null);
                        uint code = 0;
                        try { code = Convert.ToUInt32(ret); } catch { }
                        if (code == 0) return true;
                        err = "WMI 返回码 " + code;
                        break;
                    }
                }
            }
            catch (Exception ex) { err = ex.Message; }

            // 2) 兜底：winspool.drv!SetDefaultPrinter
            if (SetDefaultPrinter(printerName)) return true;

            int last = Marshal.GetLastWin32Error();
            err = err + (err.Length > 0 ? "；" : "") + "winspool 错误码 " + last;
            return false;
        }
    }

    /// <summary>
    /// 通过打印后台服务（spooler）原生 API 创建/删除标准 TCP/IP 端口。
    /// 注意：直接写注册表 Port List 键会被 ACL 拒绝（管理员也只有读权限），
    /// 正规做法是对 ",XcvMonitor Standard TCP/IP Port" 句柄调 XcvDataW。
    /// </summary>
    internal static class XcvPort
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, ref PRINTER_DEFAULTS pDefault);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        // XcvDataW 要求句柄具备 SERVER_ACCESS_ADMINISTER
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PRINTER_DEFAULTS { public IntPtr pDatatype; public IntPtr pDevMode; public uint DesiredAccess; }
        private const uint SERVER_ALL_ACCESS = 0x000F0003;   // 含 SERVER_ACCESS_ADMINISTER

        private static bool OpenMonitorHandle(out IntPtr h, out string err)
        {
            err = "";
            const string monitor = ",XcvMonitor Standard TCP/IP Port";
            var dflt = new PRINTER_DEFAULTS { pDatatype = IntPtr.Zero, pDevMode = IntPtr.Zero, DesiredAccess = SERVER_ALL_ACCESS };
            if (OpenPrinter(monitor, out h, ref dflt)) return true;
            int e1 = Marshal.GetLastWin32Error();
            if (OpenPrinter(monitor, out h, IntPtr.Zero)) return true;   // 回退默认权限
            err = "无法连接打印监视器（错误码 " + e1 + "/" + Marshal.GetLastWin32Error() + "）";
            return false;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool XcvDataW(
            IntPtr hXcv, string pszDataName,
            IntPtr pInputData, uint cbInputData,
            IntPtr pOutputData, uint cbOutputData,
            out uint pcbNeeded, out uint pdwStatus);

        // PORT_DATA_1（tcpxcv.h），对齐按 8 字节封送
        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        private struct PORT_DATA_1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string pPortName;
            public uint dwVersion;      // 2
            public uint dwProtocol;     // 1 = RAW
            public uint cbSize;         // 结构大小
            public uint dwReserved;     // mon 就绪标志，1
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szHostAddress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]  public string szSNMPCommunity;
            public uint dwDoubleHop;    // 0
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szPVQueueName;
            public uint dwPortNumber;   // 9100
            public uint dwSNMPEnabled;  // 0 = 关闭 SNMP（避免探测失败导致端口“脱机”）
            public uint dwSNMPDevIndex; // 1
        }

        public static bool PortExists(string portName, out string err)
        {
            err = "";
            IntPtr h;
            if (!OpenMonitorHandle(out h, out err)) return false;
            try
            {
                var buf = Marshal.StringToHGlobalUni(portName + "\0");
                try
                {
                    uint needed, status;
                    var outBuf = Marshal.AllocHGlobal(4);
                    try
                    {
                        bool ok = XcvDataW(h, "PortExists", buf,
                            (uint)((portName.Length + 1) * 2), outBuf, 4, out needed, out status);
                        if (!ok)
                        {
                            err = "查询端口失败（错误码 " + Marshal.GetLastWin32Error() + "）";
                            return false;
                        }
                        return Marshal.ReadInt32(outBuf) != 0 || status == 1802;
                    }
                    finally { Marshal.FreeHGlobal(outBuf); }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { ClosePrinter(h); }
        }

        // AddPort / ConfigPort 共用 PORT_DATA_1 输入；tpl 提供协议/队列/SNMP（LPR 端口必须带上，否则配置会被破坏）
        private static bool SendPortData(string command, string verb, string portName, string hostAddress, TcpPortInfo tpl, out string err)
        {
            err = "";
            // 关键：AddPort/ConfigPort 需要 SeLoadDriverPrivilege 处于「已启用」状态，否则返回 status=5
            TokenPrivilege.EnableSeLoadDriver();

            bool isLpr = tpl != null && tpl.Protocol == 2;
            uint protocol = isLpr ? 2u : 1u;
            uint portNumber = isLpr ? (tpl.PortNumber == 0 ? 515u : tpl.PortNumber) : 9100u;
            string queue = isLpr ? (string.IsNullOrEmpty(tpl.Queue) ? "BINARY_P1" : tpl.Queue) : "";
            bool snmp = tpl != null && tpl.SNMPEnabled;
            string snmpCommunity = (tpl != null && !string.IsNullOrEmpty(tpl.SNMPCommunity)) ? tpl.SNMPCommunity : "public";

            IntPtr h;
            if (!OpenMonitorHandle(out h, out err)) return false;
            try
            {
                var data = new PORT_DATA_1
                {
                    pPortName = portName,
                    dwVersion = 2,
                    dwProtocol = protocol,
                    cbSize = (uint)Marshal.SizeOf(typeof(PORT_DATA_1)),
                    dwReserved = 1,
                    szHostAddress = hostAddress,
                    szSNMPCommunity = snmpCommunity,
                    dwDoubleHop = 0,
                    szPVQueueName = queue,
                    dwPortNumber = portNumber,
                    dwSNMPEnabled = snmp ? 1u : 0u,
                    dwSNMPDevIndex = 1
                };
                IntPtr buf = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PORT_DATA_1)));
                try
                {
                    Marshal.StructureToPtr(data, buf, false);
                    uint needed, status;
                    bool ok = XcvDataW(h, command, buf,
                        (uint)Marshal.SizeOf(typeof(PORT_DATA_1)), IntPtr.Zero, 0,
                        out needed, out status);
                    if (!ok || status != 0)
                    {
                        err = verb + "失败（status=" + status + "，" + DescribeStatus(status) + "）";
                        return false;
                    }
                    return true;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { ClosePrinter(h); }
        }

        public static bool Add(string portName, string hostAddress, out string err)
        {
            return SendPortData("AddPort", "创建端口", portName, hostAddress, null, out err);
        }

        public static bool Add(string portName, string hostAddress, TcpPortInfo tpl, out string err)
        {
            return SendPortData("AddPort", "创建端口", portName, hostAddress, tpl, out err);
        }

        /// <summary>就地修正已有端口的地址（端口名不变，必须带端口原配置以保留协议/队列/SNMP）</summary>
        public static bool Configure(string portName, string hostAddress, TcpPortInfo info, out string err)
        {
            return SendPortData("ConfigPort", "修正端口地址", portName, hostAddress, info, out err);
        }

        /// <summary>删除端口（输入为端口名字符串）</summary>
        public static bool Delete(string portName, out string err)
        {
            err = "";
            TokenPrivilege.EnableSeLoadDriver();
            IntPtr h;
            if (!OpenMonitorHandle(out h, out err)) return false;
            try
            {
                IntPtr buf = Marshal.StringToHGlobalUni(portName + "\0");
                try
                {
                    uint needed, status;
                    bool ok = XcvDataW(h, "DeletePort", buf,
                        (uint)((portName.Length + 1) * 2), IntPtr.Zero, 0,
                        out needed, out status);
                    if (!ok || status != 0)
                    {
                        err = "删除端口失败（status=" + status + "，" + DescribeStatus(status) + "）";
                        return false;
                    }
                    return true;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { ClosePrinter(h); }
        }

        private static string DescribeStatus(uint s)
        {
            switch (s)
            {
                case 0: return "成功";
                case 5: return "拒绝访问";
                case 87: return "参数无效";
                case 1802: return "端口已存在";
                case 1801: return "打印机名无效";
                default: return "Win32 错误 " + s;
            }
        }
    }

    public static class IpChanger
    {
        /// <summary>
        /// 修改打印机 IP：优先「就地修改当前端口的地址」，端口名保持原名不变；
        /// 仅当原端口无法就地修改时，才退回「新建端口并切换」（端口名会变，日志中会注明）。
        /// </summary>
        public static void ChangePrinterIp(string printerName, string oldPortName, string newIp, out string via, out string err)
        {
            err = ""; via = "";
            if (string.IsNullOrEmpty(oldPortName)) { err = "打印机没有关联端口，无法修改 IP。"; return; }

            // 第一次：就地改址（端口名保持原名不变）
            if (TryChangeInPlace(printerName, oldPortName, newIp, out via, out err)) return;
            string first = err;

            // 第二次：重启 Print Spooler 后再试（spooler 会缓存端口配置，重启后常能改成功）
            ServiceHelper.RestartSpooler(out string rmsg);
            if (TryChangeInPlace(printerName, oldPortName, newIp, out via, out err))
            {
                via += "（Print Spooler 已重启后生效）";
                return;
            }

            err = "无法保持原端口名就地修改 — 第一次：" + first + "；重启服务后重试：" + err +
                  (string.IsNullOrEmpty(rmsg) ? "" : "；服务重启：" + rmsg);
        }

        /// <summary>保持原端口名不变地改地址：ConfigPort → 注册表 → 按原名重建，每步都复核</summary>
        private static bool TryChangeInPlace(string printerName, string oldPortName, string newIp,
            out string via, out string err)
        {
            via = ""; err = "";
            TcpPortInfo cur = WmiPorts.FindByName(WmiPorts.GetAll(), oldPortName);

            // 1) ConfigPort 就地改地址（端口名不变，协议/队列/SNMP 原样保留）
            //    注意：它可能「返回成功但实际未生效」，因此必须复核真实地址
            string cerr;
            if (XcvPort.Configure(oldPortName, newIp, cur, out cerr))
            {
                if (VerifyPortAddress(oldPortName, newIp))
                {
                    via = "端口 " + oldPortName + " 已就地指向新 IP（端口名保持不变）";
                    return true;
                }
                cerr = "ConfigPort 返回成功，但复核发现地址仍是 " + CurrentAddressOf(oldPortName);
            }

            // 2) 注册表直改 HostName/IPAddress（端口名不变）+ 复核
            string rerr;
            if (FixPortAddressRegistry(oldPortName, newIp, out rerr))
            {
                if (VerifyPortAddress(oldPortName, newIp))
                {
                    via = "端口 " + oldPortName + " 已就地指向新 IP（注册表路径，端口名保持不变）";
                    return true;
                }
                rerr = "注册表已写入，但打印服务读回的地址仍是 " + CurrentAddressOf(oldPortName);
            }

            // 3) 按原名重建端口（中转端口腾空 → 删旧 → 同名重建 → 切回）—— 端口名始终不变
            string berr;
            if (RebuildPortKeepName(printerName, oldPortName, newIp, cur, out berr))
            {
                if (VerifyPortAddress(oldPortName, newIp))
                {
                    via = "端口 " + oldPortName + " 已按原名重建并指向新 IP（端口名保持不变）";
                    return true;
                }
                berr = "重建后复核未生效，地址仍是 " + CurrentAddressOf(oldPortName);
            }

            err = "原生接口：" + cerr + "；注册表：" + rerr + "；原名重建：" + berr;
            return false;
        }

        /// <summary>
        /// 仅在用户明确同意后才调用：新建指向新 IP 的端口并切换打印机（端口名会变）。
        /// </summary>
        public static void ForceNewPortAndSwitch(string printerName, string oldPortName, string newIp,
            out string via, out string err)
        {
            err = ""; via = "";
            string newPort = EnsurePort(newIp, oldPortName, out string perr);
            if (newPort == null) { err = "新建端口失败：" + perr; return; }

            string werr;
            if (!SetPrinterPortWmi(printerName, newPort, out werr))
            {
                string po2;
                if (!PsHelper.SetPrinterPort(printerName, newPort, out po2))
                {
                    using (var pk = Reg.Open(Reg.PrintersKey + "\\" + printerName, true))
                    {
                        if (pk == null)
                        {
                            err = "新端口已建好（" + newPort + "），但切换打印机端口失败 — WMI：" + werr +
                                  "；PowerShell：" + po2 + "；注册表也不可写。";
                            return;
                        }
                        pk.SetValue("Port", newPort, RegistryValueKind.String);
                    }
                }
            }
            via = "已新建端口 " + newPort + " 并切换（经确认，端口名由 " + oldPortName + " 变更）";
        }

        /// <summary>
        /// 复核：从打印服务（WMI）读回端口的真实地址，确认它确实已指向 newIp。
        /// spooler 写回有短暂延迟，因此最多轮询约 1.5 秒。
        /// </summary>
        private static bool VerifyPortAddress(string portName, string ip)
        {
            for (int i = 0; i < 6; i++)
            {
                var p = WmiPorts.FindByName(WmiPorts.GetAll(), portName);
                if (p != null && !string.IsNullOrEmpty(p.HostAddress) &&
                    p.HostAddress.Trim().Equals(ip, StringComparison.OrdinalIgnoreCase))
                    return true;
                System.Threading.Thread.Sleep(250);
            }
            return false;
        }

        private static string CurrentAddressOf(string portName)
        {
            var p = WmiPorts.FindByName(WmiPorts.GetAll(), portName);
            return (p == null || string.IsNullOrEmpty(p.HostAddress)) ? "(未知)" : p.HostAddress;
        }

        /// <summary>
        /// 按原名重建端口（端口名保持绝对不变）：
        /// 建中转端口 → 打印机切到中转 → 删旧端口 → 按原名建指向新 IP 的端口 → 打印机切回原名 → 删中转。
        /// 这样既保证地址真的改掉（AddPort 已实测可用），又不会产生 x.x.x.x_2/_3 这类改名端口。
        /// </summary>
        private static bool RebuildPortKeepName(string printerName, string portName, string newIp,
            TcpPortInfo cur, out string err)
        {
            err = "";
            string tmp = portName + "_tmp";
            string fallbackAddress = cur != null ? cur.HostAddress : newIp;

            // 清掉可能残留的中转端口
            XcvPort.Delete(tmp, out string _);

            // 1) 建中转端口（继承原端口的协议/队列），并把打印机切过去腾空原端口
            if (!XcvPort.Add(tmp, newIp, cur, out string aerr))
            {
                err = "中转端口创建失败：" + aerr;
                return false;
            }

            SetPrinterPortWmi(printerName, tmp, out string _);
            PsHelper.SetPrinterPort(printerName, tmp, out string _);

            // 2) 删掉旧端口，再按原同名重建为指向新 IP
            bool deleted = XcvPort.Delete(portName, out string derr);
            if (!XcvPort.Add(portName, newIp, cur, out string aerr2))
            {
                // 建不回来就尽量恢复原状：按原地址重建同名端口并切回打印机
                if (deleted) XcvPort.Add(portName, fallbackAddress, cur, out string _);
                SetPrinterPortWmi(printerName, portName, out string _);
                PsHelper.SetPrinterPort(printerName, portName, out string _);
                XcvPort.Delete(tmp, out string _);
                err = "按原名重建失败" + (deleted ? "" : "（旧端口删除失败：" + derr + "）") + "：" + aerr2;
                return false;
            }

            // 3) 把打印机切回原端口名，并清掉中转端口
            if (!SetPrinterPortWmi(printerName, portName, out string werr))
                PsHelper.SetPrinterPort(printerName, portName, out string _);
            XcvPort.Delete(tmp, out string _);
            return true;
        }

        // 就地改址的注册表路径：只改 HostName/IPAddress 的值，端口子键名（即端口名）不动
        private static bool FixPortAddressRegistry(string portName, string newIp, out string err)
        {
            err = "";
            using (var pl = Reg.OpenPortsKey(true))
            {
                if (pl == null) { err = "注册表端口键不可写"; return false; }
                using (var pk = pl.OpenSubKey(portName, true))
                {
                    if (pk == null) { err = "注册表中未找到端口 " + portName; return false; }
                    try
                    {
                        // HostName 存实际地址；IPAddress 若存在（老系统字段）一并同步
                        pk.SetValue("HostName", newIp, RegistryValueKind.String);
                        if (Array.IndexOf(pk.GetValueNames(), "IPAddress") >= 0)
                            pk.SetValue("IPAddress", newIp, RegistryValueKind.String);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        err = "注册表写入失败：" + ex.Message;
                        return false;
                    }
                }
            }
        }

        /// <summary>找到一个实际地址 = newIp 的端口；没有则修正/新建，返回端口名</summary>
        private static string EnsurePort(string newIp, string templatePort, out string err)
        {
            err = "";
            var all = WmiPorts.GetAll();

            // 0) 模板端口信息（新建端口时继承协议/队列，如 Brother 的 LPR+BINARY_P1）
            TcpPortInfo tpl = null;
            if (!string.IsNullOrEmpty(templatePort))
                tpl = WmiPorts.FindByName(all, templatePort);

            // 1) 已有端口实际指向 newIp → 直接复用（不管端口名叫什么）
            var byAddr = WmiPorts.FindByAddress(all, newIp);
            if (byAddr != null) return byAddr.Name;

            // 2) 存在同名端口但地址不一致 → ConfigPort 就地改地址（保留协议/队列/SNMP）
            var byName = WmiPorts.FindByName(all, newIp);
            if (byName != null)
            {
                string cerr;
                if (XcvPort.Configure(newIp, newIp, byName, out cerr)) return newIp;

                // 修正失败 → 删了重建（端口被打印机占用时删除会失败）
                string derr;
                if (XcvPort.Delete(newIp, out derr))
                {
                    string aerr;
                    if (XcvPort.Add(newIp, newIp, byName, out aerr)) return newIp;
                }
                // 仍不行 → 走第 3 步用带序号的新名字
            }

            // 3) 新建端口：先 WMI（能继承 LPR 配置），再 Xcv → PowerShell → 注册表
            string name = newIp;
            for (int i = 2; WmiPorts.FindByName(all, name) != null; i++)
            {
                name = newIp + "_" + i;
                all = WmiPorts.GetAll();
            }

            string werr2;
            if (WmiPorts.Create(name, newIp, tpl, out werr2)) return name;

            string perr;
            if (XcvPort.Add(name, newIp, tpl, out perr)) return name;

            string pout;
            if (PsHelper.AddPort(name, newIp, out pout)) return name;

            string rerr;
            string regPort = CreatePortRegistry(name, templatePort, newIp, out rerr);
            if (regPort != null) return regPort;

            err = "创建端口失败 — WMI：" + werr2 + "；原生接口：" + perr +
                  "；PowerShell：" + pout + "；注册表：" + rerr;
            return null;
        }

        // 遍历端口列表，找实际地址 == ip 的端口名（只读，注册表允许）
        private static string FindPortByAddress(string ip)
        {
            using (var pl = Reg.OpenPortsKey())
            {
                if (pl == null) return null;
                foreach (var n in pl.GetSubKeyNames())
                {
                    using (var k = pl.OpenSubKey(n))
                    {
                        if (k == null) continue;
                        foreach (var vn in new[] { "HostName", "IPAddress" })
                        {
                            var v = k.GetValue(vn)?.ToString();
                            if (v != null && v.Trim().Equals(ip, StringComparison.OrdinalIgnoreCase)) return n;
                        }
                    }
                }
            }
            return null;
        }

        private static bool PortNameExists(string portName)
        {
            using (var pl = Reg.OpenPortsKey())
            {
                if (pl == null) return false;
                foreach (var n in pl.GetSubKeyNames())
                    if (string.Equals(n, portName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool SetPrinterPortWmi(string printerName, string portName, out string err)
        {
            err = "";
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_Printer"))
                {
                    foreach (System.Management.ManagementObject p in searcher.Get())
                    {
                        var name = p["Name"] as string;
                        if (name != printerName) continue;
                        p["PortName"] = portName;
                        p.Put();
                        return true;
                    }
                }
                err = "WMI 未找到打印机：" + printerName;
                return false;
            }
            catch (Exception ex)
            {
                err = "WMI 切换端口失败：" + ex.Message;
                return false;
            }
        }

        // 兜底路径：注册表直建端口（ACL 允许时才走得到），名字由调用方指定
        private static string CreatePortRegistry(string name, string templatePort, string ip, out string err)
        {
            err = "";
            using (var pl = Reg.OpenPortsKey(true))
            {
                if (pl == null)
                {
                    err = "无法打开标准 TCP/IP 端口列表（该键对管理员也是只读的，属正常限制）。";
                    return null;
                }

                RegistryKey src = null;
                if (!string.IsNullOrEmpty(templatePort)) src = pl.OpenSubKey(templatePort);
                using (src)
                using (var dst = pl.CreateSubKey(name))
                {
                    if (src != null)
                    {
                        foreach (var vn in src.GetValueNames())
                            dst.SetValue(vn, src.GetValue(vn), src.GetValueKind(vn));
                    }
                    dst.SetValue("HostName", ip, RegistryValueKind.String);
                    if (dst.GetValue("Protocol") == null) dst.SetValue("Protocol", 1, RegistryValueKind.DWord); // 1=RAW
                    if (dst.GetValue("PortNumber") == null) dst.SetValue("PortNumber", 9100, RegistryValueKind.DWord);
                }
                return name;
            }
        }
    }

    public static class Installer
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AddPrinterPackage(
            [MarshalAs(UnmanagedType.LPWStr)] string pkg,
            [In, Out] int[] requiredReboot);

        public static void InstallDriver(string path, out string msg)
        {
            msg = "";
            var ext = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            try
            {
                if (ext == ".exe")
                {
                    var psi = new ProcessStartInfo(path);
                    psi.UseShellExecute = true;
                    if (File.Exists(path)) psi.WorkingDirectory = Path.GetDirectoryName(path);
                    Process.Start(psi);
                    msg = "✓ 已启动安装程序：" + Path.GetFileName(path) + "。请在弹出的窗口中完成安装，然后点【刷新列表】。";
                }
                else if (ext == ".zip")
                {
                    int[] r = { 0 };
                    if (AddPrinterPackage(path, r))
                        msg = "✓ 驱动包已安装：" + Path.GetFileName(path);
                    else
                        msg = "⚠ 安装驱动包失败（错误码 " + Marshal.GetLastWin32Error() + "）。请确认文件为 v4 驱动包 .zip 且来源可信。";
                }
                else if (ext == ".inf")
                {
                    msg = "检测到 .inf：请通过 Windows【设置 → 打印机】→ 添加打印机 → 手动安装驱动 完成。";
                }
                else
                {
                    msg = "不支持的文件类型：" + ext;
                }
            }
            catch (Exception ex)
            {
                msg = "⚠ 启动失败：" + ex.Message;
            }
        }
    }

    public static class ServiceHelper
    {
        public static void SetStartupAutomatic()
        {
            using (var key = Reg.Open(@"SYSTEM\CurrentControlSet\Services\Spooler", true))
            {
                if (key != null) key.SetValue("Start", 2, RegistryValueKind.DWord);
            }
        }

        public static void RestartSpooler(out string msg)
        {
            msg = "";
            try
            {
                SetStartupAutomatic();   // 顺带确保启动类型为「自动」
                var sc = new ServiceController("Spooler");
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                msg = "重启服务失败：" + ex.Message;
            }
        }
    }

    public static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
