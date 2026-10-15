using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminPages : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Pages");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--pg-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("pg-menu", Auth.Menu("pg", "AdminPages", c, site));
            response.SetElementContents("pg-who", Auth.Who("pg", c));
            response.SetElementContents("pg-demo", Auth.DemoPill("pg", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminPagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminPagesJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"pg-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "pg-p" : "pg-y") : string.Empty) + "\">" + ch + "</i>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }

        private static List<(string Key, string Name, string Url)> Editable(Ctx c, SiteData site)
        {
            List<(string, string, string)> list = new List<(string, string, string)>();
            if (c.U.Admin || c.U.FrontPage)
            {
                list.Add(("home", "Front page", "Home"));
            }
            foreach (Desk d in site.Desks.Where(d => Access.DeskPublisher(c.U, d)))
            {
                list.Add((d.Key, d.Name, "Section?d=" + Uri.EscapeDataString(d.Key)));
            }
            return list;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            List<(string Key, string Name, string Url)> pages = Editable(c, site);
            if (pages.Count == 0)
            {
                response.SetElementContents("pg-main", "<div class=\"pg-ptop\"><div><h1>Pages</h1></div></div><div class=\"pg-info-box\">Arranging a page needs Publish rights on every section of a department, or front-page rights for the front page.</div>");
                await Store.Log(c.Sbx, c.U.Id, "page", "open", false);
                return response;
            }
            string pk = Ui.Clip(GetDataValue("pk"), 40);
            if (!pages.Any(p => p.Key == pk))
            {
                pk = pages[0].Key;
            }
            response.SetElementContents("pg-main", await Editor(c, site, pages, pk));
            response.SetElementContents("pg-mbody", StylePicker());
            response.ExecuteScript("AdminPagesJs.loaded();");
            return response;
        }

        private static string StylePicker()
        {
            StringBuilder sb = new StringBuilder("<h2>Add a section</h2><div class=\"pg-styles\">");
            foreach ((string k, string n, string hint) in Ui.Styles)
            {
                sb.Append("<button type=\"button\" class=\"pg-sty\" onclick=\"AdminPagesJs.add('" + k + "')\">" + Thumb(k) + "<b>" + n + "</b><small>" + hint + "</small></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private async Task<string> Editor(Ctx c, SiteData site, List<(string Key, string Name, string Url)> pages, string pk)
        {
            Dictionary<string, List<PageModule>> all = await Store.LoadPages(c.Sbx);
            List<PageModule> mods = all.TryGetValue(pk, out List<PageModule>? l) ? l : new List<PageModule>();
            (string Key, string Name, string Url) cur = pages.First(p => p.Key == pk);
            StringBuilder sb = new StringBuilder("<div class=\"pg-ptop\"><div><h1>Pages</h1><p>Stack sections from top to bottom. Each one fills itself with the newest published stories from its source.</p></div></div><div class=\"pg-tabs\">");
            foreach ((string k, string n, string u) in pages)
            {
                sb.Append("<button type=\"button\" class=\"pg-tab" + (k == pk ? " pg-act" : string.Empty) + "\" onclick=\"AdminPagesJs.go('" + k + "')\">" + Ui.Enc(n) + "</button>");
            }
            sb.Append("</div><input type=\"hidden\" id=\"pg-pk\" value=\"" + Ui.Attr(pk) + "\"><div class=\"pg-lbar\"><div class=\"pg-acts\"><b>" + Ui.Enc(cur.Name) + "</b><span class=\"pg-dirty\" id=\"pg-dirty\"></span></div><div class=\"pg-acts\"><a class=\"pg-btn pg-btn-sm pg-btn-ghost\" href=\"" + cur.Url + "\" target=\"_blank\" rel=\"noopener\">View page</a><button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" onclick=\"AdminPagesJs.restore()\">Restore last version</button><button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-accent\" onclick=\"AdminPagesJs.save()\">Save page</button></div></div><div class=\"pg-canvas\" id=\"pg-canvas\">");
            foreach (PageModule m in mods)
            {
                sb.Append(CardHtml(site, pk, m));
            }
            sb.Append("</div><div class=\"pg-add\"><button type=\"button\" onclick=\"AdminPagesJs.pickStyle()\">+ Add a section</button></div>");
            return sb.ToString();
        }

        private static string StyleName(string style)
        {
            return Ui.Styles.FirstOrDefault(s => s.Key == style).Name ?? style;
        }

        private static string SourceOptions(SiteData site, string pk, string selected)
        {
            StringBuilder sb = new StringBuilder();
            List<(string v, string n)> top = new List<(string, string)>();
            if (pk == "home")
            {
                top.Add(("all", "All sections"));
                top.Add(("front", "Front-page picks"));
            }
            foreach ((string v, string n) in top)
            {
                sb.Append("<option value=\"" + v + "\"" + (v == selected ? " selected" : string.Empty) + ">" + n + "</option>");
            }
            foreach (Desk d in site.Desks.Where(d => pk == "home" || d.Key == pk))
            {
                sb.Append("<optgroup label=\"" + Ui.Attr(d.Name) + "\"><option value=\"d:" + Ui.Attr(d.Key) + "\"" + ("d:" + d.Key == selected ? " selected" : string.Empty) + ">All of " + Ui.Enc(d.Name) + "</option>");
                foreach (DeskSection s in d.Sections)
                {
                    sb.Append("<option value=\"s:" + Ui.Attr(s.Key) + "\"" + ("s:" + s.Key == selected ? " selected" : string.Empty) + ">" + Ui.Enc(s.Name) + "</option>");
                }
                sb.Append("</optgroup>");
            }
            return sb.ToString();
        }

        private static bool SourceAllowed(SiteData site, string pk, string source)
        {
            if (!Ui.ValidSource(site, source))
            {
                return false;
            }
            if (pk == "home")
            {
                return true;
            }
            if (source.StartsWith("d:", StringComparison.Ordinal))
            {
                return source.Substring(2) == pk;
            }
            if (source.StartsWith("s:", StringComparison.Ordinal))
            {
                return Ui.DeskOf(site, source.Substring(2))?.Key == pk;
            }
            return false;
        }

        private static string PinOptions(SiteData site, string source, string selected)
        {
            StringBuilder sb = new StringBuilder("<option value=\"\">Newest first</option>");
            PageModule probe = new PageModule { Style = "briefs", Source = source, Count = 12 };
            List<Story> list = Ui.Pick(site, probe, new HashSet<string>());
            Story? pinned = selected != string.Empty ? Ui.LiveStories(site).FirstOrDefault(s => s.Id == selected) : null;
            if (pinned != null && !list.Contains(pinned))
            {
                list.Insert(0, pinned);
            }
            foreach (Story s in list)
            {
                string t = s.Title.Length > 60 ? s.Title.Substring(0, 58) + "…" : s.Title;
                sb.Append("<option value=\"" + Ui.Attr(s.Id) + "\"" + (s.Id == selected ? " selected" : string.Empty) + ">" + Ui.Enc(t) + "</option>");
            }
            return sb.ToString();
        }

        private static string CardHtml(SiteData site, string pk, PageModule m)
        {
            string id = Ui.Attr(m.Id);
            StringBuilder sb = new StringBuilder("<div class=\"pg-blk\" data-id=\"" + id + "\" data-style=\"" + Ui.Attr(m.Style) + "\"><div class=\"pg-btool\"><button type=\"button\" class=\"pg-grip\" aria-label=\"Drag to reorder\" title=\"Drag to reorder\" onpointerdown=\"AdminPagesJs.drag(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button><span class=\"pg-bname\">" + Ui.Enc(StyleName(m.Style)) + "</span><button type=\"button\" aria-label=\"Move up\" title=\"Move up\" onclick=\"AdminPagesJs.move(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" aria-label=\"Move down\" title=\"Move down\" onclick=\"AdminPagesJs.move(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button><button type=\"button\" class=\"pg-del\" aria-label=\"Remove section\" title=\"Remove section\" onclick=\"AdminPagesJs.remove(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button></div>");
            sb.Append("<div class=\"pg-bbody\"><div class=\"pg-bthumb\">" + Thumb(m.Style) + "</div><div class=\"pg-bf\">");
            sb.Append("<label class=\"pg-field pg-f-title" + (Ui.TextStyles.Contains(m.Style) ? " pg-wide2" : string.Empty) + "\" for=\"pg-t-" + id + "\"><span>Heading</span><input type=\"text\" id=\"pg-t-" + id + "\" maxlength=\"60\" value=\"" + Ui.Attr(m.Title) + "\" placeholder=\"" + (m.Style == "lead" || m.Style == "leadwide" ? "Optional" : "Section heading") + "\"></label>");
            if (Ui.TextStyles.Contains(m.Style))
            {
                if (m.Style != "follow" || true)
                {
                    sb.Append("<label class=\"pg-field pg-f-text pg-wide2\" for=\"pg-x-" + id + "\"><span>" + (m.Style == "notice" ? "Text (blank line for a new paragraph)" : "Short text") + "</span><textarea id=\"pg-x-" + id + "\" rows=\"" + (m.Style == "notice" ? 4 : 2) + "\" maxlength=\"600\">" + Ui.Enc(m.Text) + "</textarea></label>");
                }
                if (m.Style == "follow")
                {
                    sb.Append("<p class=\"pg-hint pg-wide2\">Shows the social links switched on in Settings.</p>");
                }
            }
            else
            {
                (int min, int max) = Ui.CountRange(m.Style);
                int n = Math.Clamp(m.Count <= 0 ? Ui.DefaultCount(m.Style) : m.Count, min, max);
                sb.Append("<label class=\"pg-field pg-f-src\" for=\"pg-s-" + id + "\"><span>Stories from</span><select id=\"pg-s-" + id + "\" onchange=\"AdminPagesJs.src(this)\">" + SourceOptions(site, pk, m.Source) + "</select></label>");
                sb.Append("<label class=\"pg-field pg-f-count\" for=\"pg-n-" + id + "\"><span>Stories</span><select id=\"pg-n-" + id + "\"" + (min == max ? " disabled" : string.Empty) + ">");
                for (int i = min; i <= max; i++)
                {
                    sb.Append("<option value=\"" + i + "\"" + (i == n ? " selected" : string.Empty) + ">" + i + "</option>");
                }
                sb.Append("</select></label>");
                if (m.Style != "ticker" && m.Style != "mostread")
                {
                    sb.Append("<label class=\"pg-field pg-f-pin pg-wide2\" for=\"pg-pin-" + id + "\"><span>Pin a story to the top</span><select id=\"pg-pin-" + id + "\">" + PinOptions(site, m.Source, m.Pin) + "</select></label>");
                }
            }
            sb.Append("</div></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Card()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string pk = Ui.Clip(GetDataValue("pk"), 40);
            string style = GetDataValue("style");
            if (!Ui.Styles.Any(s => s.Key == style))
            {
                return response;
            }
            if (!Access.CanPage(c.U, pk, site))
            {
                return await Refuse(response, c, "page", pk, "You can't arrange this page.");
            }
            string source = pk == "home" ? "all" : "d:" + pk;
            PageModule m = new PageModule { Id = "m" + Guid.NewGuid().ToString("N").Substring(0, 10), Style = style, Source = source, Count = Ui.DefaultCount(style), Title = Ui.TextStyles.Contains(style) ? StyleName(style) : style == "lead" || style == "leadwide" ? string.Empty : "Latest" };
            if (style == "newsletter")
            {
                m.Title = "The Morning Post";
                m.Text = "One email at 7am with the stories that matter.";
            }
            if (style == "follow")
            {
                m.Title = "Follow the Post";
            }
            response.ExecuteScript("document.getElementById('pg-canvas').insertAdjacentHTML('beforeend'," + System.Text.Json.JsonSerializer.Serialize(CardHtml(site, pk, m)) + ");AdminPagesJs.added('" + m.Id + "');");
            return response;
        }

        public async Task<ApiResponse> Pins()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string mod = System.Text.RegularExpressions.Regex.IsMatch(GetDataValue("mod"), "^[A-Za-z0-9]{1,20}$") ? GetDataValue("mod") : string.Empty;
            string source = Ui.Clip(GetDataValue("source"), 60);
            if (mod == string.Empty || !Ui.ValidSource(site, source))
            {
                return response;
            }
            response.SetElementContents("pg-pin-" + mod, PinOptions(site, source, string.Empty));
            return response;
        }

        private sealed class ModIn
        {
            public string Id { get; set; } = string.Empty;
            public string Style { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public int Count { get; set; }
            public string Pin { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string pk = Ui.Clip(GetDataValue("pk"), 40);
            if (!Access.CanPage(c.U, pk, site))
            {
                return await Refuse(response, c, "page", pk, "You can't arrange this page.");
            }
            List<ModIn>? input;
            try
            {
                input = System.Text.Json.JsonSerializer.Deserialize<List<ModIn>>(GetDataValue("mods"), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException)
            {
                input = null;
            }
            if (input == null || input.Count > 30)
            {
                response.ExecuteScript("AdminPagesJs.toast('" + (input == null ? "Could not read the page." : "A page can have up to 30 sections.") + "');");
                return response;
            }
            HashSet<string> live = new HashSet<string>(Ui.LiveStories(site).Select(s => s.Id));
            HashSet<string> ids = new HashSet<string>();
            List<PageModule> mods = new List<PageModule>();
            foreach (ModIn x in input)
            {
                if (!Ui.Styles.Any(s => s.Key == x.Style))
                {
                    continue;
                }
                string id = System.Text.RegularExpressions.Regex.IsMatch(x.Id ?? string.Empty, "^[A-Za-z0-9]{1,20}$") && !ids.Contains(x.Id!) ? x.Id! : "m" + Guid.NewGuid().ToString("N").Substring(0, 10);
                ids.Add(id);
                PageModule m = new PageModule { Id = id, Style = x.Style, Title = Ui.Clip(x.Title, 60) };
                if (Ui.TextStyles.Contains(x.Style))
                {
                    m.Source = string.Empty;
                    m.Text = Ui.Clip(x.Text, 600);
                }
                else
                {
                    m.Source = SourceAllowed(site, pk, x.Source ?? string.Empty) ? x.Source! : (pk == "home" ? "all" : "d:" + pk);
                    (int min, int max) = Ui.CountRange(x.Style);
                    m.Count = Math.Clamp(x.Count <= 0 ? Ui.DefaultCount(x.Style) : x.Count, min, max);
                    m.Pin = live.Contains(x.Pin ?? string.Empty) && x.Style != "ticker" && x.Style != "mostread" ? x.Pin! : string.Empty;
                }
                mods.Add(m);
            }
            using (await Store.Hold())
            {
                Dictionary<string, List<PageModule>> all = await Store.LoadPages(c.Sbx);
                all[pk] = mods;
                await Store.SavePages(all, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "page", pk, true);
            response.SetElementContents("pg-main", await Editor(c, site, Editable(c, site), pk));
            response.ExecuteScript("AdminPagesJs.loaded();AdminPagesJs.toast('Page saved');");
            return response;
        }

        public async Task<ApiResponse> Restore()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string pk = Ui.Clip(GetDataValue("pk"), 40);
            if (!Access.CanPage(c.U, pk, site))
            {
                return await Refuse(response, c, "page-restore", pk, "You can't arrange this page.");
            }
            bool done = false;
            using (await Store.Hold())
            {
                Dictionary<string, List<PageModule>>? prev = await Store.PreviousPages(c.Sbx);
                if (prev != null && prev.TryGetValue(pk, out List<PageModule>? old))
                {
                    Dictionary<string, List<PageModule>> all = await Store.LoadPages(c.Sbx);
                    all[pk] = old;
                    await Store.SavePages(all, c.Sbx);
                    done = true;
                }
            }
            if (!done)
            {
                response.ExecuteScript("AdminPagesJs.toast('There is no earlier version yet.');");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "page-restore", pk, true);
            response.SetElementContents("pg-main", await Editor(c, site, Editable(c, site), pk));
            response.ExecuteScript("AdminPagesJs.loaded();AdminPagesJs.toast('Earlier version restored');");
            return response;
        }

        private static string Thumb(string style)
        {
            return style switch
            {
                "lead" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"8\" width=\"70\" height=\"40\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"6\" y=\"52\" width=\"10\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"6\" y=\"57\" width=\"66\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"62\" width=\"66\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"67\" width=\"39.6\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"84\" y=\"8\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"84\" y=\"13\" width=\"30\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"84\" y=\"18\" width=\"18.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"84\" y=\"30\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"84\" y=\"35\" width=\"30\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"84\" y=\"40\" width=\"18.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"84\" y=\"52\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"84\" y=\"57\" width=\"30\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"84\" y=\"62\" width=\"18.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"8\" width=\"0.8\" height=\"64\" rx=\"1.5\" fill=\"#C9C5BE\"/></svg>",
                "leadwide" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"8\" width=\"108\" height=\"64\" rx=\"1.5\" fill=\"#7F8A96\"/><rect x=\"12\" y=\"46\" width=\"10\" height=\"2.4\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"12\" y=\"52\" width=\"70\" height=\"3.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"12\" y=\"59\" width=\"50\" height=\"3.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"12\" y=\"66\" width=\"40\" height=\"2\" rx=\"1.5\" fill=\"#E0E0E0\"/></svg>",
                "spotlight" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"14\" width=\"62\" height=\"52\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"74\" y=\"22\" width=\"10\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"74\" y=\"28\" width=\"40\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"74\" y=\"34\" width=\"40\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"74\" y=\"40\" width=\"24.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"74\" y=\"50\" width=\"36\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/><rect x=\"74\" y=\"55\" width=\"21.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/></svg>",
                "duo" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"8\" width=\"51\" height=\"34\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"6\" y=\"46\" width=\"10\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"6\" y=\"51\" width=\"48\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"56\" width=\"48\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"61\" width=\"28.799999999999997\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"63\" y=\"8\" width=\"51\" height=\"34\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"63\" y=\"46\" width=\"10\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"63\" y=\"51\" width=\"48\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"63\" y=\"56\" width=\"48\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"63\" y=\"61\" width=\"28.799999999999997\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/></svg>",
                "split" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"8\" width=\"58\" height=\"36\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"6\" y=\"48\" width=\"10\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"6\" y=\"53\" width=\"54\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"58\" width=\"54\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"63\" width=\"32.4\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"70\" y=\"8\" width=\"16\" height=\"12\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"90\" y=\"9\" width=\"24\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"90\" y=\"14\" width=\"14.399999999999999\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"70\" y=\"30\" width=\"16\" height=\"12\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"90\" y=\"31\" width=\"24\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"90\" y=\"36\" width=\"14.399999999999999\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"70\" y=\"52\" width=\"16\" height=\"12\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"90\" y=\"53\" width=\"24\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"90\" y=\"58\" width=\"14.399999999999999\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/></svg>",
                "rundown" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"16\" width=\"24\" height=\"18\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"6\" y=\"38\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"6\" y=\"43\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"48\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"53\" width=\"13.2\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"34\" y=\"16\" width=\"24\" height=\"18\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"34\" y=\"38\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"34\" y=\"43\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"34\" y=\"48\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"34\" y=\"53\" width=\"13.2\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"62\" y=\"16\" width=\"24\" height=\"18\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"62\" y=\"38\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"62\" y=\"43\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"62\" y=\"48\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"62\" y=\"53\" width=\"13.2\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"90\" y=\"16\" width=\"24\" height=\"18\" rx=\"1.5\" fill=\"#CFCAC2\"/><rect x=\"90\" y=\"38\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"90\" y=\"43\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"90\" y=\"48\" width=\"22\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"90\" y=\"53\" width=\"13.2\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/></svg>",
                "briefs" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"8\" width=\"33\" height=\"0.8\" rx=\"1.5\" fill=\"#C9C5BE\"/><rect x=\"6\" y=\"13\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"6\" y=\"18\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"23\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"28\" width=\"18.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"8\" width=\"33\" height=\"0.8\" rx=\"1.5\" fill=\"#C9C5BE\"/><rect x=\"43\" y=\"13\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"43\" y=\"18\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"23\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"28\" width=\"18.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"8\" width=\"33\" height=\"0.8\" rx=\"1.5\" fill=\"#C9C5BE\"/><rect x=\"80\" y=\"13\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"80\" y=\"18\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"23\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"28\" width=\"18.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"42\" width=\"33\" height=\"0.8\" rx=\"1.5\" fill=\"#C9C5BE\"/><rect x=\"6\" y=\"47\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"6\" y=\"52\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"57\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"62\" width=\"18.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"42\" width=\"33\" height=\"0.8\" rx=\"1.5\" fill=\"#C9C5BE\"/><rect x=\"43\" y=\"47\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"43\" y=\"52\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"57\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"62\" width=\"18.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"42\" width=\"33\" height=\"0.8\" rx=\"1.5\" fill=\"#C9C5BE\"/><rect x=\"80\" y=\"47\" width=\"8\" height=\"2\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"80\" y=\"52\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"57\" width=\"31\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"62\" width=\"18.599999999999998\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/></svg>",
                "strip" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"10\" width=\"34\" height=\"60\" rx=\"1.5\" fill=\"#7F8A96\"/><rect x=\"10\" y=\"54\" width=\"22\" height=\"2.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"10\" y=\"60\" width=\"16\" height=\"2.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"44\" y=\"10\" width=\"34\" height=\"60\" rx=\"1.5\" fill=\"#7F8A96\"/><rect x=\"48\" y=\"54\" width=\"22\" height=\"2.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"48\" y=\"60\" width=\"16\" height=\"2.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"82\" y=\"10\" width=\"34\" height=\"60\" rx=\"1.5\" fill=\"#7F8A96\"/><rect x=\"86\" y=\"54\" width=\"22\" height=\"2.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"86\" y=\"60\" width=\"16\" height=\"2.4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"112\" y=\"10\" width=\"8\" height=\"60\" rx=\"1.5\" fill=\"#B9C0C8\"/></svg>",
                "opinion" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><circle cx=\"14\" cy=\"20\" r=\"7\" fill=\"#6E8A74\"/><rect x=\"24\" y=\"17\" width=\"14\" height=\"2\" rx=\"1.5\" fill=\"#8E8A83\"/><rect x=\"6\" y=\"34\" width=\"32\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"6\" y=\"40\" width=\"32\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"6\" y=\"46\" width=\"19.2\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"6\" y=\"54\" width=\"30\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/><rect x=\"6\" y=\"59\" width=\"18.0\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/><circle cx=\"51\" cy=\"20\" r=\"7\" fill=\"#6E8A74\"/><rect x=\"61\" y=\"17\" width=\"14\" height=\"2\" rx=\"1.5\" fill=\"#8E8A83\"/><rect x=\"43\" y=\"34\" width=\"32\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"43\" y=\"40\" width=\"32\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"43\" y=\"46\" width=\"19.2\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"43\" y=\"54\" width=\"30\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/><rect x=\"43\" y=\"59\" width=\"18.0\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/><circle cx=\"88\" cy=\"20\" r=\"7\" fill=\"#6E8A74\"/><rect x=\"98\" y=\"17\" width=\"14\" height=\"2\" rx=\"1.5\" fill=\"#8E8A83\"/><rect x=\"80\" y=\"34\" width=\"32\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"80\" y=\"40\" width=\"32\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"80\" y=\"46\" width=\"19.2\" height=\"2.2\" rx=\"1\" fill=\"#5E5A54\"/><rect x=\"80\" y=\"54\" width=\"30\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/><rect x=\"80\" y=\"59\" width=\"18.0\" height=\"2.2\" rx=\"1\" fill=\"#C9C5BE\"/></svg>",
                "mostread" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><text x=\"6\" y=\"34\" font-family=\"Georgia\" font-weight=\"700\" font-size=\"16\" fill=\"#B4232A\">1</text><rect x=\"16\" y=\"22\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"16\" y=\"27\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"16\" y=\"32\" width=\"6.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><text x=\"29\" y=\"34\" font-family=\"Georgia\" font-weight=\"700\" font-size=\"16\" fill=\"#B4232A\">2</text><rect x=\"39\" y=\"22\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"39\" y=\"27\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"39\" y=\"32\" width=\"6.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><text x=\"52\" y=\"34\" font-family=\"Georgia\" font-weight=\"700\" font-size=\"16\" fill=\"#B4232A\">3</text><rect x=\"62\" y=\"22\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"62\" y=\"27\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"62\" y=\"32\" width=\"6.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><text x=\"75\" y=\"34\" font-family=\"Georgia\" font-weight=\"700\" font-size=\"16\" fill=\"#B4232A\">4</text><rect x=\"85\" y=\"22\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"85\" y=\"27\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"85\" y=\"32\" width=\"6.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><text x=\"98\" y=\"34\" font-family=\"Georgia\" font-weight=\"700\" font-size=\"16\" fill=\"#B4232A\">5</text><rect x=\"108\" y=\"22\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"108\" y=\"27\" width=\"10\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"108\" y=\"32\" width=\"6.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/></svg>",
                "ticker" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"32\" width=\"108\" height=\"16\" rx=\"1.5\" fill=\"#F1EFEB\"/><rect x=\"6\" y=\"32\" width=\"22\" height=\"16\" rx=\"1.5\" fill=\"#222222\"/><rect x=\"32\" y=\"38\" width=\"6\" height=\"2.4\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"40\" y=\"38\" width=\"30\" height=\"2.4\" rx=\"1.5\" fill=\"#8E8A83\"/><rect x=\"76\" y=\"38\" width=\"6\" height=\"2.4\" rx=\"1.5\" fill=\"#B4232A\"/><rect x=\"84\" y=\"38\" width=\"28\" height=\"2.4\" rx=\"1.5\" fill=\"#8E8A83\"/></svg>",
                "notice" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"18\" width=\"108\" height=\"44\" rx=\"1.5\" fill=\"#F1EFEB\"/><rect x=\"6\" y=\"18\" width=\"3\" height=\"44\" rx=\"0\" fill=\"#B4232A\"/><rect x=\"16\" y=\"26\" width=\"40\" height=\"3\" rx=\"1.5\" fill=\"#3A3733\"/><rect x=\"16\" y=\"36\" width=\"90\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"16\" y=\"42\" width=\"90\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"16\" y=\"48\" width=\"90\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/><rect x=\"16\" y=\"54\" width=\"54.0\" height=\"2.2\" rx=\"1\" fill=\"#8E8A83\"/></svg>",
                "newsletter" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"6\" y=\"16\" width=\"108\" height=\"48\" rx=\"1.5\" fill=\"#1E1E1E\"/><rect x=\"14\" y=\"28\" width=\"36\" height=\"4\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"14\" y=\"38\" width=\"38\" height=\"2.2\" rx=\"1\" fill=\"#9A9A9A\"/><rect x=\"14\" y=\"43\" width=\"22.8\" height=\"2.2\" rx=\"1\" fill=\"#9A9A9A\"/><rect x=\"62\" y=\"34\" width=\"32\" height=\"10\" rx=\"1.5\" fill=\"#FFFFFF\"/><rect x=\"96\" y=\"34\" width=\"14\" height=\"10\" rx=\"1.5\" fill=\"#B4232A\"/></svg>",
                "follow" => "<svg viewBox=\"0 0 120 80\" width=\"120\" height=\"80\" aria-hidden=\"true\"><rect width=\"120\" height=\"80\" fill=\"#FFFFFF\"/><rect x=\"40\" y=\"18\" width=\"40\" height=\"4\" rx=\"1.5\" fill=\"#3A3733\"/><circle cx=\"24\" cy=\"48\" r=\"6.5\" fill=\"none\" stroke=\"#8E8A83\" stroke-width=\"1.6\"/><circle cx=\"42\" cy=\"48\" r=\"6.5\" fill=\"none\" stroke=\"#8E8A83\" stroke-width=\"1.6\"/><circle cx=\"60\" cy=\"48\" r=\"6.5\" fill=\"none\" stroke=\"#8E8A83\" stroke-width=\"1.6\"/><circle cx=\"78\" cy=\"48\" r=\"6.5\" fill=\"none\" stroke=\"#8E8A83\" stroke-width=\"1.6\"/><circle cx=\"96\" cy=\"48\" r=\"6.5\" fill=\"none\" stroke=\"#8E8A83\" stroke-width=\"1.6\"/></svg>",
                _ => string.Empty
            };
        }
    }
}
