using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TicketToRide.Application.Net.Protocol;
using TicketToRide.Presentation;

public class MenuManager : MonoBehaviour
{
    private const float PanelWidth = 460f;
    private const float PanelHeight = 540f;
    private const float RowHeight = 44f;

    /// <summary>窗口化时可选的窗口大小。</summary>
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
    };

    private InputField _ipField;
    private InputField _portField;
    private InputField _nicknameField;
    private Button _displayModeButton;
    private Button _resolutionButton;

    private bool _fullscreen = true;
    private int _resolutionIndex = 2;

    private void Awake()
    {
        // 「单机」按钮是场景里手摆的，这里只补联机那一块（与其它面板一样运行时生成，不动场景文件）。
        BuildNetworkPanel();

        // 只刷新按钮文字，不碰窗口：启动时绝不擅自改分辨率或全屏状态，
        // 只有玩家自己点了按钮才会应用。否则一进游戏窗口尺寸就被改掉，界面会被裁。
        RefreshDisplayLabels();
    }

    /// <summary>把当前的显示设置应用到窗口：全屏走 FullscreenWindow，窗口化按所选分辨率开窗。</summary>
    private void ApplyDisplaySettings()
    {
        var resolution = Resolutions[_resolutionIndex];
        Screen.SetResolution(
            resolution.x,
            resolution.y,
            _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        RefreshDisplayLabels();
    }

    private void RefreshDisplayLabels()
    {
        if (_displayModeButton != null)
        {
            _displayModeButton.GetComponentInChildren<Text>().text = _fullscreen ? "显示：全屏（点击切窗口化）" : "显示：窗口化（点击切全屏）";
        }

        if (_resolutionButton != null)
        {
            var resolution = Resolutions[_resolutionIndex];
            _resolutionButton.GetComponentInChildren<Text>().text = $"窗口大小：{resolution.x} × {resolution.y}";
        }
    }

    private void OnDisplayModeClicked()
    {
        _fullscreen = !_fullscreen;
        ApplyDisplaySettings();
    }

    private void OnResolutionClicked()
    {
        _resolutionIndex = (_resolutionIndex + 1) % Resolutions.Length;
        ApplyDisplaySettings();
    }

    public void OnSinglePlayerClick()
    {
        ApplyNickname();
        NetworkBootstrap.ConfigureSingleplayer();
        SceneManager.LoadScene("GameScene");
    }

    private void OnHostClick()
    {
        if (!TryReadPort(out var port))
        {
            return;
        }

        ApplyNickname();
        NetworkBootstrap.ConfigureHost(port);
        Debug.Log($"[MenuManager] 以主机身份进入游戏，监听端口 {port}。");
        SceneManager.LoadScene("GameScene");
    }

    private void OnJoinClick()
    {
        if (!TryReadPort(out var port))
        {
            return;
        }

        ApplyNickname();
        NetworkBootstrap.ConfigureClient(_ipField.text, port);
        Debug.Log($"[MenuManager] 以客户端身份加入 {NetworkBootstrap.Ip}:{port}。");
        SceneManager.LoadScene("GameScene");
    }

    /// <summary>把输入框里的昵称记到 NetworkBootstrap（空则用默认名字）。</summary>
    private void ApplyNickname()
    {
        NetworkBootstrap.SetNickname(_nicknameField != null ? _nicknameField.text : null);
    }

    /// <summary>
    /// 本机局域网 IPv4（取第一个非回环地址），主机把它告诉朋友填进「加入」的 IP 栏。
    /// 跨网玩家需要虚拟局域网（ZeroTier 等），这里只反映本机网卡地址。
    /// </summary>
    private static string DescribeLocalIp()
    {
        try
        {
            foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    return address.ToString();
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[MenuManager] 获取本机局域网 IP 失败：{exception.Message}");
        }

        return "未检测到（可用 ipconfig 查看）";
    }

    /// <summary>读取端口输入框；非法时在 Console 里提示并返回 false。</summary>
    private bool TryReadPort(out int port)
    {
        if (int.TryParse(_portField.text.Trim(), out port) && port >= 1 && port <= 65535)
        {
            return true;
        }

        Debug.LogWarning("[MenuManager] 端口不合法，请填 1–65535 之间的数字。");
        return false;
    }

    private void BuildNetworkPanel()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[MenuManager] 场景里没有 Canvas，联机菜单无法生成。");
            return;
        }

        var panelObject = new GameObject("NetworkMenuPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);

        var panel = (RectTransform)panelObject.transform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(360f, 0f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);

        var title = ClaimPanel.CreateLabelPublic(panel, "Title", 24, TextAnchor.MiddleCenter);
        title.text = "联机";
        title.color = Color.white;
        LayoutTopStretch(title.rectTransform, -20f, 34f);

        _ipField = BuildRow(panel, "IP", -78f, NetworkBootstrap.DefaultIp, "例如 127.0.0.1");
        _portField = BuildRow(panel, "端口", -140f, NetworkBootstrap.DefaultPort.ToString(), "例如 7777");
        _nicknameField = BuildRow(panel, "昵称", -202f, NetworkBootstrap.Nickname, "留空则用默认名字");

        var hint = ClaimPanel.CreateLabelPublic(panel, "Hint", 14, TextAnchor.MiddleCenter);
        hint.text = "昵称对所有模式生效；主机只需填端口，加入需填主机 IP 与端口\n" +
                    $"本机局域网 IP：{DescribeLocalIp()}（主机把这一串告诉朋友）";
        hint.color = new Color(1f, 1f, 1f, 0.6f);
        LayoutTopStretch(hint.rectTransform, -256f, 44f);

        _displayModeButton = ClaimPanel.CreateButtonPublic(panel, "显示", new Color(0.35f, 0.35f, 0.4f, 1f));
        _displayModeButton.onClick.AddListener(OnDisplayModeClicked);
        LayoutTopStretch((RectTransform)_displayModeButton.transform, -312f, 44f);

        _resolutionButton = ClaimPanel.CreateButtonPublic(panel, "窗口大小", new Color(0.35f, 0.35f, 0.4f, 1f));
        _resolutionButton.onClick.AddListener(OnResolutionClicked);
        LayoutTopStretch((RectTransform)_resolutionButton.transform, -364f, 44f);

        var hostButton = ClaimPanel.CreateButtonPublic(panel, "主机（开房间）", new Color(0.2f, 0.45f, 0.9f, 1f));
        hostButton.onClick.AddListener(OnHostClick);
        LayoutBottom(hostButton.transform, 24f, 200f);

        var joinButton = ClaimPanel.CreateButtonPublic(panel, "加入（连接）", new Color(0.25f, 0.6f, 0.35f, 1f));
        joinButton.onClick.AddListener(OnJoinClick);
        LayoutBottom(joinButton.transform, 240f, 196f);
    }

    /// <summary>一行「标签 + 输入框」，纵向位置以面板顶边为基准。</summary>
    private static InputField BuildRow(Transform parent, string labelText, float topOffset, string value, string placeholder)
    {
        var label = ClaimPanel.CreateLabelPublic(parent, labelText + "Label", 16, TextAnchor.MiddleLeft);
        label.text = labelText;
        label.color = Color.white;
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(24f, topOffset);
        label.rectTransform.sizeDelta = new Vector2(80f, RowHeight);

        var field = CreateInputField(parent, labelText + "Field", value, placeholder);
        var fieldRect = (RectTransform)field.transform;
        fieldRect.anchorMin = new Vector2(0f, 1f);
        fieldRect.anchorMax = new Vector2(0f, 1f);
        fieldRect.pivot = new Vector2(0f, 1f);
        fieldRect.anchoredPosition = new Vector2(112f, topOffset);
        fieldRect.sizeDelta = new Vector2(PanelWidth - 136f, RowHeight);

        return field;
    }

    private static void LayoutTopStretch(RectTransform rect, float topOffset, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, topOffset);
        rect.sizeDelta = new Vector2(-40f, height);
    }

    private static void LayoutBottom(Transform buttonTransform, float leftOffset, float width)
    {
        var rect = (RectTransform)buttonTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(leftOffset, 24f);
        rect.sizeDelta = new Vector2(width, 58f);
    }

    /// <summary>运行时代码生成输入框（UnityEngine.UI.InputField 需要额外的文本与占位子节点）。</summary>
    private static InputField CreateInputField(Transform parent, string name, string value, string placeholderText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);

        var background = go.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.95f);

        var text = CreateFieldLabel(go.transform, "Text", Color.black);
        var placeholder = CreateFieldLabel(go.transform, "Placeholder", new Color(0f, 0f, 0f, 0.35f));
        placeholder.text = placeholderText;

        var field = go.GetComponent<InputField>();
        field.targetGraphic = background;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = InputField.LineType.SingleLine;
        field.characterLimit = MessageCodec.MaxNicknameLength + 8;
        field.text = value;

        return field;
    }

    private static Text CreateFieldLabel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.supportRichText = false;

        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 0f);
        rect.offsetMax = new Vector2(-10f, 0f);

        return text;
    }
}