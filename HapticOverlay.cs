using UnityEngine;

namespace LovenseRoR2;

internal static class HapticOverlay
{
    private static Texture2D _bgTex    = null!;
    private static Texture2D _barBgTex = null!;
    private static Texture2D _fillTex  = null!;
    private static GUIStyle  _titleStyle = null!;
    private static GUIStyle  _pctStyle   = null!;
    private static bool _stylesReady;

    private const int PanelW  = 180;
    private const int PanelH  = 34;
    private const int Padding = 6;
    private const int BarH    = 4;

    internal static void Initialize()
    {
        _bgTex    = MakeTex(new Color(0f,    0f,    0f,    0.5f));
        _barBgTex = MakeTex(new Color(0.15f, 0.15f, 0.15f, 0.9f));
        _fillTex  = MakeTex(Color.white); // tinted per-draw via GUI.color
    }

    internal static void Draw()
    {
        if (!PluginConfig.ShowOverlay.Value) return;

        // GUIStyles must be created after Unity skin is ready (not in Initialize)
        if (!_stylesReady)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 10,
                normal    = { textColor = new Color(0.8f, 0.8f, 0.8f) },
            };
            _pctStyle = new GUIStyle(_titleStyle)
            {
                fontSize  = 12,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = Color.white },
            };
            _stylesReady = true;
        }

        int x = Mathf.Clamp(PluginConfig.OverlayX.Value, 0, Mathf.Max(0, Screen.width - PanelW));
        int y = Mathf.Clamp(PluginConfig.OverlayY.Value, 0, Mathf.Max(0, Screen.height - PanelH));

        // background panel
        GUI.DrawTexture(new Rect(x, y, PanelW, PanelH), _bgTex);

        bool preview = HapticSettings.PreviewOnly.Value;
        bool connected = preview || LovensePlugin.ToyId != null;
        bool second = preview || LovensePlugin.ToyId2 != null;
        bool paused = LovensePlugin.Paused;
        string caption = paused ? "PAUSED" : preview ? "PREVIEW" : connected ? "LOVENSE" : "OFFLINE";
        string percent = !connected || paused ? "—" : second
            ? $"{LovensePlugin.DevicePercent[0]}% · {LovensePlugin.DevicePercent[1]}%"
            : $"{LovensePlugin.DevicePercent[0]}%";
        GUI.Label(new Rect(x + Padding, y + 3, 60, 19), caption, _titleStyle);
        GUI.Label(new Rect(x + 66, y + 3, PanelW - 66 - Padding, 19), percent, _pctStyle);

        int slots = second ? 2 : 1;
        int gap = second ? 4 : 0;
        int barW = (PanelW - Padding * 2 - gap) / slots;
        for (int slot = 0; slot < slots; slot++)
        {
            int barX = x + Padding + slot * (barW + gap);
            int barY = y + PanelH - Padding - BarH;
            GUI.DrawTexture(new Rect(barX, barY, barW, BarH), _barBgTex);
            float pct = connected && !paused ? LovensePlugin.DevicePercent[slot] / 100f : 0;
            if (pct <= 0) continue;
            var oldColor = GUI.color;
            GUI.color = Color.Lerp(new Color(1f, 0.55f, 0.78f), new Color(1f, 0.08f, 0.42f), pct);
            GUI.DrawTexture(new Rect(barX, barY, barW * pct, BarH), _fillTex);
            GUI.color = oldColor;
        }
    }

    internal static void Dispose()
    {
        Object.Destroy(_bgTex);
        Object.Destroy(_barBgTex);
        Object.Destroy(_fillTex);
        _stylesReady = false;
    }

    private static Texture2D MakeTex(Color color)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }
}
