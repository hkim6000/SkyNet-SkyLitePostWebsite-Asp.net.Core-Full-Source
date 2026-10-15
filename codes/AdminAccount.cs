using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminAccount : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | My account");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--my-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("my-menu", Auth.Menu("my", "AdminAccount", c, site));
            response.SetElementContents("my-who", Auth.Who("my", c));
            response.SetElementContents("my-demo", Auth.DemoPill("my", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminAccountJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAccountJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"my-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "my-p" : "my-y") : string.Empty) + "\">" + ch + "</i>");
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
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            response.SetElementContents("my-main", await MainHtml(c, site));
            response.ExecuteScript("AdminAccountJs.loaded();");
            return response;
        }

        private async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Staff u = c.U;
            StringBuilder sb = new StringBuilder("<div class=\"my-ptop\"><div><h1>My account</h1><p>Your byline, bio and password.</p></div><div class=\"my-acts\"><a class=\"my-btn my-btn-sm my-btn-ghost\" href=\"Author?u=" + Uri.EscapeDataString(u.Id) + "\" target=\"_blank\" rel=\"noopener\">View my author page</a></div></div><div class=\"my-acc2\">");
            sb.Append("<section class=\"my-panel\"><div class=\"my-me\">" + Ui.Avatar("my", u, "md") + "<div><b>" + Ui.Enc(u.Name) + "</b><small>" + Ui.Enc(u.Login) + (u.Admin ? " &middot; editor-in-chief" : string.Empty) + "</small></div></div><form class=\"my-form2\" style=\"display:grid;gap:14px\" onsubmit=\"return AdminAccountJs.profile()\" novalidate><div class=\"my-g2\"><label class=\"my-field\" for=\"my-mname\"><span>Name on your byline</span><input type=\"text\" id=\"my-mname\" maxlength=\"60\" value=\"" + Ui.Attr(u.Name) + "\"><em class=\"my-err\" id=\"my-e-mname\"></em></label><label class=\"my-field\" for=\"my-mtitle\"><span>Job title</span><input type=\"text\" id=\"my-mtitle\" maxlength=\"60\" value=\"" + Ui.Attr(u.Title) + "\"></label></div><label class=\"my-field\" for=\"my-memail\"><span>Email</span><input type=\"email\" id=\"my-memail\" maxlength=\"80\" value=\"" + Ui.Attr(u.Email) + "\"><em class=\"my-err\" id=\"my-e-memail\"></em></label><label class=\"my-field\" for=\"my-mbio\"><span>Short bio</span><textarea id=\"my-mbio\" rows=\"4\" maxlength=\"400\">" + Ui.Enc(u.Bio) + "</textarea></label><div><button type=\"submit\" class=\"my-btn\">Save profile</button></div></form></section>");
            sb.Append("<section class=\"my-panel\"><h2>Change password</h2><form style=\"display:grid;gap:14px\" onsubmit=\"return AdminAccountJs.password()\" novalidate><label class=\"my-field\" for=\"my-cur\"><span>Current password</span><input type=\"password\" id=\"my-cur\" maxlength=\"100\" autocomplete=\"current-password\"><em class=\"my-err\" id=\"my-e-cur\"></em></label><label class=\"my-field\" for=\"my-nw\"><span>New password</span><input type=\"password\" id=\"my-nw\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"my-err\" id=\"my-e-nw\"></em></label><label class=\"my-field\" for=\"my-nw2\"><span>New password again</span><input type=\"password\" id=\"my-nw2\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"my-err\" id=\"my-e-nw2\"></em></label><div><button type=\"submit\" class=\"my-btn\">Change password</button></div>" + (Store.Demo ? "<p class=\"my-hint\">Demo: the change only applies to your private copy.</p>" : string.Empty) + "</form></section></div>");
            return sb.ToString();
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

        public async Task<ApiResponse> Profile()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            string name = Regex.Replace(Ui.Clip(GetDataValue("mname"), 80), "\\s+", " ");
            string email = Ui.Clip(GetDataValue("memail"), 100);
            string eName = name.Length < 2 || name.Length > 60 ? "Enter a name (2 to 60 characters)." : string.Empty;
            string eEmail = email != string.Empty && !IsEmail(email) ? "Enter a valid email or leave it blank." : string.Empty;
            response.SetElementContents("my-e-mname", eName);
            response.SetElementContents("my-e-memail", eEmail);
            if (eName + eEmail != string.Empty)
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff(c.Sbx);
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u != null)
                {
                    u.Name = name;
                    u.Title = Ui.Clip(GetDataValue("mtitle"), 60);
                    u.Email = email;
                    u.Bio = Ui.Clip(GetDataValue("mbio"), 400);
                    await Store.SaveStaff(staff, c.Sbx);
                    c.U = u;
                }
            }
            await Store.Log(c.Sbx, c.U.Id, "account", "profile", true);
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            response.SetElementContents("my-main", await MainHtml(c, site));
            response.ExecuteScript("AdminAccountJs.toast('Profile saved');");
            return response;
        }

        public async Task<ApiResponse> Password()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            string cur = Ui.Clip(GetDataValue("cur"), 200);
            string nw = Ui.Clip(GetDataValue("nw"), 200);
            string nw2 = Ui.Clip(GetDataValue("nw2"), 200);
            bool curOk = Access.Verify(cur, c.U.Hash) || (!Store.Demo && c.U.Login == Store.Cfg("app.admin.user").ToLowerInvariant() && Access.VerifySha(cur, Store.Cfg("app.admin.passhash")));
            string eCur = !curOk ? "That is not your current password." : string.Empty;
            string eNw = nw.Length < 8 ? "Use at least 8 characters." : string.Empty;
            string eNw2 = eNw == string.Empty && nw != nw2 ? "The two new passwords don't match." : string.Empty;
            response.SetElementContents("my-e-cur", eCur);
            response.SetElementContents("my-e-nw", eNw);
            response.SetElementContents("my-e-nw2", eNw2);
            if (eCur + eNw + eNw2 != string.Empty)
            {
                if (eCur != string.Empty)
                {
                    await Store.Log(c.Sbx, c.U.Id, "password", c.U.Login, false);
                }
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff(c.Sbx);
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u != null)
                {
                    u.Hash = Access.HashPassword(nw);
                    u.Demo = false;
                    await Store.SaveStaff(staff, c.Sbx);
                }
            }
            await Auth.CloseUser(c.Sbx, c.U.Id, Store.CleanId(GetDataValue("tok")));
            await Store.Log(c.Sbx, c.U.Id, "password", c.U.Login, true);
            response.ExecuteScript("AdminAccountJs.changed();AdminAccountJs.toast('Password changed');");
            return response;
        }
    }
}
