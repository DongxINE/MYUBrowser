namespace MYUBrowser.Core;

/// <summary>注入网页的伪装/控制脚本与样式</summary>
public static class DisguiseScripts
{
    /// <summary>暂停页面内所有 video/audio，并递归处理同源 iframe</summary>
    public const string PauseAllMedia = """
        (function () {
            function pause(doc) {
                try {
                    doc.querySelectorAll('video,audio').forEach(function (m) { try { m.pause(); } catch (e) { } });
                    doc.querySelectorAll('iframe').forEach(function (f) {
                        try { if (f.contentDocument) pause(f.contentDocument); } catch (e) { }
                    });
                } catch (e) { }
            }
            pause(document);
        })();
        """;

    /// <summary>深色滤镜：整页反色变暗，图片/视频再反转回来保持正常观感</summary>
    public const string DarkFilterCss = """
        html { filter: invert(0.92) hue-rotate(180deg) !important; background: #101010 !important; }
        img, video, picture, canvas, iframe, embed, object, svg image {
            filter: invert(1) hue-rotate(180deg) !important;
        }
        """;

    /// <summary>低调模式：图片/视频灰度化并大幅淡化，页面看起来像纯文字资料</summary>
    public const string HideImagesCss = """
        img, video, picture, svg, canvas, embed, object {
            opacity: 0.07 !important;
            filter: grayscale(1) !important;
            transition: opacity .15s;
        }
        img:hover, video:hover, picture:hover, canvas:hover { opacity: 1 !important; filter: none !important; }
        """;

    /// <summary>按 id 注入或移除一段 style，enable 为 js 布尔字面量</summary>
    public static string ToggleStyle(string id, string css, bool enable)
    {
        var cssJson = System.Text.Json.JsonSerializer.Serialize(css);
        var idJson = System.Text.Json.JsonSerializer.Serialize(id);
        var enableJs = enable ? "true" : "false";
        return $$"""
            (function () {
                var id = {{idJson}};
                var old = document.getElementById(id);
                if ({{enableJs}}) {
                    if (!old) {
                        var s = document.createElement('style');
                        s.id = id;
                        s.textContent = {{cssJson}};
                        (document.head || document.documentElement).appendChild(s);
                    }
                } else if (old) {
                    old.remove();
                }
            })();
            """;
    }

    /// <summary>
    /// 按当前快捷键绑定动态生成网页内监听脚本：命中则 postMessage('myu:cmd:&lt;id&gt;')。
    /// 脚本可重复注入（会先移除上一版监听，避免重复触发）。
    /// </summary>
    public static string BuildHotkeyListener(IEnumerable<(string Id, Keys Gesture)> bindings)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("(function(){");
        sb.Append("if(window.__myuHK){try{window.removeEventListener('keydown',window.__myuHK,true);}catch(e){}}");
        sb.Append("window.__myuHK=function(e){");
        foreach (var (id, g) in bindings)
        {
            string? keyCond = JsKeyCondition(g & Keys.KeyCode);
            if (keyCond == null) continue;
            string ctrl = ((g & Keys.Control) != 0) ? "true" : "false";
            string alt = ((g & Keys.Alt) != 0) ? "true" : "false";
            string shift = ((g & Keys.Shift) != 0) ? "true" : "false";
            sb.Append($"if(e.ctrlKey==={ctrl}&&e.altKey==={alt}&&e.shiftKey==={shift}&&{keyCond}){{");
            sb.Append("e.preventDefault();e.stopPropagation();");
            sb.Append($"try{{window.chrome.webview.postMessage('myu:cmd:{id}');}}catch(err){{}}return;}}");
        }
        sb.Append("};");
        sb.Append("window.addEventListener('keydown',window.__myuHK,true);");
        sb.Append("})();");
        return sb.ToString();
    }

    /// <summary>把键位转成 JS 中对 e.key 的判断；无法映射的返回 null（该键仅在应用聚焦时生效）。</summary>
    private static string? JsKeyCondition(Keys key)
    {
        switch (key)
        {
            case Keys.Up: return "e.key==='ArrowUp'";
            case Keys.Down: return "e.key==='ArrowDown'";
            case Keys.Left: return "e.key==='ArrowLeft'";
            case Keys.Right: return "e.key==='ArrowRight'";
        }
        if (key >= Keys.A && key <= Keys.Z)
        {
            char c = (char)('a' + (key - Keys.A));
            return $"(e.key==='{c}'||e.key==='{char.ToUpper(c)}')";
        }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            char c = (char)('0' + (key - Keys.D0));
            return $"e.key==='{c}'";
        }
        return null;
    }
}
