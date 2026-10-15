using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminSettings : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Settings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--st-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("st-menu", Auth.Menu("st", "AdminSettings", c, site));
            response.SetElementContents("st-who", Auth.Who("st", c));
            response.SetElementContents("st-demo", Auth.DemoPill("st", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminSettingsJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"st-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "st-p" : "st-y") : string.Empty) + "\">" + ch + "</i>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (!c.U.Admin)
            {
                await Store.Log(c.Sbx, c.U.Id, "settings", "open", false);
                response.SetElementContents("st-main", "<div class=\"st-ptop\"><div><h1>Settings</h1></div></div><div class=\"st-info-box\">Only the editor-in-chief can change the paper&rsquo;s settings.</div>");
                return response;
            }
            response.SetElementContents("st-main", MainHtml(site));
            response.ExecuteScript("AdminSettingsJs.loaded();");
            return response;
        }

        private static string MainHtml(SiteData site)
        {
            Settings s = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"st-ptop\"><div><h1>Settings</h1><p>The paper&rsquo;s name, contact details and social links.</p></div></div><form class=\"st-sform\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel\"><h2>The paper</h2><div class=\"st-form2\" style=\"display:grid;gap:14px\"><div class=\"st-g2\"><label class=\"st-field\" for=\"st-sname\"><span>Name</span><input type=\"text\" id=\"st-sname\" maxlength=\"40\" value=\"" + Ui.Attr(s.Name) + "\"><em class=\"st-err\" id=\"st-e-sname\"></em></label><label class=\"st-field\" for=\"st-stag\"><span>Tagline</span><input type=\"text\" id=\"st-stag\" maxlength=\"90\" value=\"" + Ui.Attr(s.Tagline) + "\"></label></div>");
            sb.Append("<div class=\"st-g2\"><label class=\"st-field\" for=\"st-saccent\"><span>Accent colour</span><span class=\"st-colr\"><input type=\"color\" aria-label=\"Pick colour\" value=\"" + Ui.Attr(Regex.IsMatch(s.Accent, "^#[0-9a-fA-F]{6}$") ? s.Accent : "#B4232A") + "\" oninput=\"AdminSettingsJs.color(this.value)\"><input type=\"text\" id=\"st-saccent\" maxlength=\"7\" value=\"" + Ui.Attr(s.Accent) + "\"></span><em class=\"st-err\" id=\"st-e-saccent\"></em></label><label class=\"st-field\" for=\"st-stheme\"><span>Default theme</span><select id=\"st-stheme\"><option value=\"light\"" + (s.Theme != "dark" ? " selected" : string.Empty) + ">Light</option><option value=\"dark\"" + (s.Theme == "dark" ? " selected" : string.Empty) + ">Dark</option></select></label></div>");
            sb.Append("<label class=\"st-chk\"><input type=\"checkbox\" id=\"st-scomments\"" + (s.Comments ? " checked" : string.Empty) + "> Allow reader comments (each one is moderated)</label></div></section>");
            sb.Append("<section class=\"st-panel\"><h2>Contact and about</h2><div style=\"display:grid;gap:14px\"><div class=\"st-g3\"><label class=\"st-field\" for=\"st-semail\"><span>Email</span><input type=\"email\" id=\"st-semail\" maxlength=\"80\" value=\"" + Ui.Attr(s.Email) + "\"><em class=\"st-err\" id=\"st-e-semail\"></em></label><label class=\"st-field\" for=\"st-sphone\"><span>Phone</span><input type=\"text\" id=\"st-sphone\" maxlength=\"30\" value=\"" + Ui.Attr(s.Phone) + "\"></label><label class=\"st-field\" for=\"st-saddr\"><span>Address</span><input type=\"text\" id=\"st-saddr\" maxlength=\"120\" value=\"" + Ui.Attr(s.Address) + "\"></label></div>");
            sb.Append("<label class=\"st-field\" for=\"st-sabout\"><span>About the Post (blank line for a new paragraph)</span><textarea id=\"st-sabout\" rows=\"7\" maxlength=\"3000\">" + Ui.Enc(s.About) + "</textarea></label></div></section>");
            sb.Append("<section class=\"st-panel\"><h2>Social links</h2><p class=\"st-hint\" style=\"margin-bottom:8px\">Ticked links show under the footer menu and in Follow sections. Use full https:// addresses.</p>");
            foreach ((string k, string n) in Social.Networks)
            {
                SocialLink? l = s.Social.FirstOrDefault(x => x.Key == k);
                sb.Append("<div class=\"st-soc-r\">" + Social.Icon(k) + "<label for=\"st-so-" + k + "\"><b>" + n + "</b></label><input type=\"url\" id=\"st-so-" + k + "\" maxlength=\"200\" placeholder=\"https://\" value=\"" + Ui.Attr(l?.Url ?? string.Empty) + "\"><label class=\"st-chk\"><input type=\"checkbox\" id=\"st-sh-" + k + "\"" + (l != null && l.Show ? " checked" : string.Empty) + "> Show</label></div>");
            }
            sb.Append("<em class=\"st-err\" id=\"st-e-ssocial\"></em></section><div class=\"st-sbar\"><button type=\"submit\" class=\"st-btn st-btn-accent\">Save settings</button></div></form>");
            return sb.ToString();
        }

        private sealed class SocIn
        {
            public string Key { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public bool Show { get; set; }
        }

        private static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            if (!c.U.Admin)
            {
                return await Refuse(response, c, "settings", "save", "Only the editor-in-chief can change settings.");
            }
            string name = Regex.Replace(Ui.Clip(GetDataValue("sname"), 60), "\\s+", " ");
            string accent = Ui.Clip(GetDataValue("saccent"), 10);
            string email = Ui.Clip(GetDataValue("semail"), 100);
            List<SocIn> soc;
            try
            {
                soc = System.Text.Json.JsonSerializer.Deserialize<List<SocIn>>(GetDataValue("ssocial"), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<SocIn>();
            }
            catch (System.Text.Json.JsonException)
            {
                soc = new List<SocIn>();
            }
            string eName = name.Length < 2 || name.Length > 40 ? "Enter a name (2 to 40 characters)." : string.Empty;
            string eAccent = !Regex.IsMatch(accent, "^#[0-9a-fA-F]{6}$") ? "Use a colour like #B4232A." : string.Empty;
            string eEmail = email != string.Empty && !IsEmail(email) ? "Enter a valid email or leave it blank." : string.Empty;
            List<string> bad = soc.Where(x => Social.Networks.Any(n => n.Key == x.Key) && (x.Url ?? string.Empty).Trim() != string.Empty && !Social.SafeUrl(x.Url!.Trim())).Select(x => Social.Networks.First(n => n.Key == x.Key).Name).ToList();
            string eSoc = bad.Count > 0 ? "Check these links: " + string.Join(", ", bad) + " (full https:// address)." : string.Empty;
            response.SetElementContents("st-e-sname", eName);
            response.SetElementContents("st-e-saccent", eAccent);
            response.SetElementContents("st-e-semail", eEmail);
            response.SetElementContents("st-e-ssocial", eSoc);
            if (eName + eAccent + eEmail + eSoc != string.Empty)
            {
                response.ExecuteScript("AdminSettingsJs.toast('Please check the highlighted fields');");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Settings s = site.Settings;
                s.Name = name;
                s.Tagline = Ui.Clip(GetDataValue("stag"), 90);
                s.Accent = accent.ToUpperInvariant();
                s.Theme = GetDataValue("stheme") == "dark" ? "dark" : "light";
                s.Email = email;
                s.Phone = Ui.Clip(GetDataValue("sphone"), 30);
                s.Address = Ui.Clip(GetDataValue("saddr"), 120);
                s.About = Ui.Clip(GetDataValue("sabout"), 3000);
                s.Comments = GetDataValue("scomments") == "1";
                s.Social = Social.Networks.Select(n => soc.FirstOrDefault(x => x.Key == n.Key)).Where(x => x != null && (x.Url ?? string.Empty).Trim() != string.Empty).Select(x => new SocialLink { Key = x!.Key, Url = x.Url.Trim(), Show = x.Show }).ToList();
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "settings", "save", true);
            response.ExecuteScript("AdminSettingsJs.toast('Settings saved');");
            return response;
        }
    }
}
