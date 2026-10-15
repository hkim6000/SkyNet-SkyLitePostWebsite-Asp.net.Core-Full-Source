using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminUsers : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Users");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--us-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("us-menu", Auth.Menu("us", "AdminUsers", c, site));
            response.SetElementContents("us-who", Auth.Who("us", c));
            response.SetElementContents("us-demo", Auth.DemoPill("us", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminUsersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminUsersJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"us-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "us-p" : "us-y") : string.Empty) + "\">" + ch + "</i>");
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
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (!c.U.Admin)
            {
                await Store.Log(c.Sbx, c.U.Id, "users", "open", false);
                response.SetElementContents("us-main", "<div class=\"us-ptop\"><div><h1>Users</h1></div></div><div class=\"us-info-box\">Only the editor-in-chief can manage users and permissions.</div>");
                return response;
            }
            response.SetElementContents("us-main", MainHtml(site));
            (string rows, string pager) = await Rows(c, site, string.Empty, string.Empty, "1");
            response.SetElementContents("us-ulist", rows);
            response.SetElementContents("us-upager", pager);
            response.ExecuteScript("AdminUsersJs.loaded();");
            return response;
        }

        private static string MainHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"us-ptop\"><div><h1>Users</h1><p>Set what each person can do, section by section.</p></div><div class=\"us-acts\"><button type=\"button\" class=\"us-btn us-btn-accent\" onclick=\"AdminUsersJs.edit('')\">Add a user</button></div></div>");
            sb.Append("<div class=\"us-ufilters\"><input type=\"search\" id=\"us-uq\" maxlength=\"60\" placeholder=\"Search name, user name or title\" aria-label=\"Search users\" oninput=\"AdminUsersJs.search()\"><select id=\"us-ud\" aria-label=\"Department\" onchange=\"AdminUsersJs.list(1)\"><option value=\"\">All departments</option>");
            foreach (Desk d in site.Desks)
            {
                sb.Append("<option value=\"" + Ui.Attr(d.Key) + "\">" + Ui.Enc(d.Name) + "</option>");
            }
            sb.Append("</select></div><div class=\"us-ulist\" id=\"us-ulist\"></div><div id=\"us-upager\"></div>");
            return sb.ToString();
        }

        private const int PageSize = 20;

        private async Task<(string Rows, string Pager)> Rows(Ctx c, SiteData site, string q, string dept, string pg)
        {
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            IEnumerable<Staff> found = staff;
            if (q != string.Empty)
            {
                found = found.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Login.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            if (dept != string.Empty)
            {
                found = found.Where(x => x.Dept == dept);
            }
            List<Staff> all = found.OrderByDescending(x => x.Active).ThenByDescending(x => x.Admin).ThenBy(x => x.Name).ToList();
            if (all.Count == 0)
            {
                return ("<div class=\"us-ur\"><span class=\"us-none\">No users match.</span></div>", string.Empty);
            }
            int page = Ui.PageOf(pg, PageSize, all.Count);
            StringBuilder sb = new StringBuilder();
            foreach (Staff u in all.Skip((page - 1) * PageSize).Take(PageSize))
            {
                string roles;
                if (u.Admin)
                {
                    roles = "<b>Everything</b> &middot; users, settings, front page";
                }
                else
                {
                    List<string> parts = new List<string>();
                    foreach (Desk d in site.Desks)
                    {
                        List<string> secs = d.Sections.Select(s => (s, p: Access.Perm(u, s.Key))).Where(x => x.p != string.Empty).Select(x => x.s.Name + " " + (x.p.Contains('P') ? "P" : x.p)).ToList();
                        if (Access.DeskPublisher(u, d))
                        {
                            parts.Add("<b>" + Ui.Enc(d.Name) + "</b> publisher");
                        }
                        else if (secs.Count > 0)
                        {
                            parts.Add("<b>" + Ui.Enc(d.Name) + "</b> " + Ui.Enc(string.Join(", ", secs)));
                        }
                    }
                    if (u.FrontPage)
                    {
                        parts.Add("front page");
                    }
                    roles = parts.Count > 0 ? string.Join(" &middot; ", parts) : "No access yet";
                }
                sb.Append("<div class=\"us-ur" + (u.Active ? string.Empty : " us-off") + "\">" + Ui.Avatar("us", u) + "<div><b>" + Ui.Enc(u.Name) + "</b><small>" + Ui.Enc(u.Title) + " &middot; " + Ui.Enc(u.Login) + "</small></div><div class=\"us-roles\">" + roles + "</div><span class=\"us-c-st\">" + (u.Active ? (u.Demo && !Store.Demo ? "<span class=\"us-st us-st-review\">Needs password</span>" : "<span class=\"us-st us-st-published\">Active</span>") : "<span class=\"us-st\">Off</span>") + "</span><button type=\"button\" class=\"us-btn us-btn-sm us-btn-ghost\" onclick=\"AdminUsersJs.edit('" + Ui.Attr(u.Id) + "')\">Edit</button></div>");
            }
            return (sb.ToString(), Ui.Pager("us", page, PageSize, all.Count, "user", "users", "AdminUsersJs.list"));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            if (!c.U.Admin)
            {
                return await Refuse(response, c, "users", "list", "Only the editor-in-chief can manage users.");
            }
            (string rows, string pager) = await Rows(c, site, Ui.Clip(GetDataValue("uq"), 60).Trim(), Ui.Clip(GetDataValue("ud"), 40), GetDataValue("pg"));
            response.SetElementContents("us-ulist", rows);
            response.SetElementContents("us-upager", pager);
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            if (!c.U.Admin)
            {
                return await Refuse(response, c, "user", "form", "Only the editor-in-chief can manage users.");
            }
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            string id = Ui.Clip(GetDataValue("uid"), 40);
            Staff? u = id != string.Empty ? staff.FirstOrDefault(x => x.Id == id) : null;
            bool isNew = u == null;
            u ??= new Staff { Active = true, Dept = site.Desks.FirstOrDefault()?.Key ?? string.Empty };
            bool self = u.Id == c.U.Id;
            StringBuilder sb = new StringBuilder("<h2>" + (isNew ? "Add a user" : "Edit " + Ui.Enc(u.Name)) + "</h2><form class=\"us-uform\" onsubmit=\"return AdminUsersJs.save()\" novalidate><input type=\"hidden\" id=\"us-u-id\" value=\"" + Ui.Attr(isNew ? string.Empty : u.Id) + "\">");
            sb.Append("<div class=\"us-g3\"><label class=\"us-field\" for=\"us-u-name\"><span>Full name</span><input type=\"text\" id=\"us-u-name\" maxlength=\"60\" value=\"" + Ui.Attr(u.Name) + "\"><em class=\"us-err\" id=\"us-e-u-name\"></em></label>");
            sb.Append("<label class=\"us-field\" for=\"us-u-login\"><span>User name</span><input type=\"text\" id=\"us-u-login\" maxlength=\"30\" autocapitalize=\"none\" value=\"" + Ui.Attr(u.Login) + "\"" + (isNew ? string.Empty : " readonly") + "><em class=\"us-err\" id=\"us-e-u-login\"></em></label>");
            sb.Append("<label class=\"us-field\" for=\"us-u-title\"><span>Job title</span><input type=\"text\" id=\"us-u-title\" maxlength=\"60\" value=\"" + Ui.Attr(u.Title) + "\"></label></div>");
            sb.Append("<div class=\"us-g3\"><label class=\"us-field\" for=\"us-u-dept\"><span>Department</span><select id=\"us-u-dept\">");
            foreach (Desk d in site.Desks)
            {
                sb.Append("<option value=\"" + Ui.Attr(d.Key) + "\"" + (d.Key == u.Dept ? " selected" : string.Empty) + ">" + Ui.Enc(d.Name) + "</option>");
            }
            sb.Append("</select></label><label class=\"us-field\" for=\"us-u-email\"><span>Email</span><input type=\"email\" id=\"us-u-email\" maxlength=\"80\" value=\"" + Ui.Attr(u.Email) + "\"><em class=\"us-err\" id=\"us-e-u-email\"></em></label>");
            sb.Append("<label class=\"us-field\" for=\"us-u-pass\"><span>" + (isNew ? "Password" : "New password (optional)") + "</span><input type=\"password\" id=\"us-u-pass\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"us-err\" id=\"us-e-u-pass\"></em></label></div>");
            sb.Append("<label class=\"us-field\" for=\"us-u-bio\"><span>Short bio (shown on the author page)</span><textarea id=\"us-u-bio\" rows=\"2\" maxlength=\"400\">" + Ui.Enc(u.Bio) + "</textarea></label>");
            sb.Append("<div class=\"us-flags2\"><label class=\"us-chk\"><input type=\"checkbox\" id=\"us-u-admin\" onchange=\"AdminUsersJs.admin()\"" + (u.Admin ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "> Editor-in-chief (every permission)</label><label class=\"us-chk\"><input type=\"checkbox\" id=\"us-u-front\"" + (u.FrontPage ? " checked" : string.Empty) + "> Can arrange the front page</label><label class=\"us-chk\"><input type=\"checkbox\" id=\"us-u-active\"" + (u.Active ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "> Account active</label></div>");
            sb.Append("<div><b>Permissions by section</b><p class=\"us-hint\">Publish includes everything else for that section: publishing, editing and deleting any story, and moderating comments. Publish on every section of a department also lets them arrange that department&rsquo;s page.</p></div>");
            sb.Append("<div class=\"us-gridwrap\" id=\"us-gridwrap\"><table class=\"us-grid\"><thead><tr><th>Section</th><th>V<span>View</span></th><th>U<span>Upload</span></th><th>E<span>Edit</span></th><th>D<span>Delete</span></th><th>P<span>Publish</span></th></tr></thead><tbody>");
            foreach (Desk d in site.Desks)
            {
                sb.Append("<tr class=\"us-dk\"><td>" + Ui.Enc(d.Name) + "</td><td colspan=\"5\" style=\"text-align:right\"><label><input type=\"checkbox\" data-for=\"" + Ui.Attr(d.Key) + "\" onchange=\"AdminUsersJs.desk(this)\"" + (Access.DeskPublisher(u, d) && !u.Admin ? " checked" : string.Empty) + "> Department publisher</label></td></tr>");
                foreach (DeskSection s in d.Sections)
                {
                    string p = u.Admin ? string.Empty : Access.Perm(u, s.Key);
                    sb.Append("<tr><td>" + Ui.Enc(s.Name) + "</td>");
                    foreach (char ch in Access.Letters)
                    {
                        sb.Append("<td><input type=\"checkbox\" aria-label=\"" + Ui.Attr(s.Name) + " " + ch + "\" data-sec=\"" + Ui.Attr(s.Key) + "\" data-desk=\"" + Ui.Attr(d.Key) + "\" data-l=\"" + ch + "\" onchange=\"AdminUsersJs.tick(this)\"" + (p.Contains(ch) ? " checked" : string.Empty) + "></td>");
                    }
                    sb.Append("</tr>");
                }
            }
            sb.Append("</tbody></table></div><em class=\"us-err\" id=\"us-e-u-form\"></em><div class=\"us-acts\"><button type=\"submit\" class=\"us-btn us-btn-accent\">" + (isNew ? "Add user" : "Save changes") + "</button><button type=\"button\" class=\"us-btn us-btn-ghost\" onclick=\"AdminUsersJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("us-mbody", sb.ToString());
            response.ExecuteScript("AdminUsersJs.formed();");
            return response;
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

        public async Task<ApiResponse> SaveUser()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            if (!c.U.Admin)
            {
                return await Refuse(response, c, "user", Ui.Clip(GetDataValue("ulogin"), 30), "Only the editor-in-chief can manage users.");
            }
            string id = Ui.Clip(GetDataValue("uid"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("uname"), 80), "\\s+", " ");
            string login = Ui.Clip(GetDataValue("ulogin"), 40).ToLowerInvariant();
            string title = Ui.Clip(GetDataValue("utitle"), 60);
            string dept = site.Desks.Any(d => d.Key == GetDataValue("udept")) ? GetDataValue("udept") : site.Desks.FirstOrDefault()?.Key ?? string.Empty;
            string email = Ui.Clip(GetDataValue("uemail"), 100);
            string bio = Ui.Clip(GetDataValue("ubio"), 400);
            string pass = Ui.Clip(GetDataValue("upass"), 200);
            Dictionary<string, string> raw;
            try
            {
                raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(GetDataValue("uperms")) ?? new Dictionary<string, string>();
            }
            catch (System.Text.Json.JsonException)
            {
                raw = new Dictionary<string, string>();
            }
            Dictionary<string, string> perms = new Dictionary<string, string>();
            foreach (DeskSection s in site.Desks.SelectMany(d => d.Sections))
            {
                if (!raw.TryGetValue(s.Key, out string? v) || v == null)
                {
                    continue;
                }
                string p = new string(Access.Letters.Where(ch => v.Contains(ch)).ToArray());
                if (p.Contains('P'))
                {
                    p = Access.Letters;
                }
                else if (p.Length > 0 && !p.Contains('V'))
                {
                    p = "V" + p;
                }
                if (p.Length > 0)
                {
                    perms[s.Key] = p;
                }
            }
            string eName = name.Length < 2 ? "Enter a name." : name.Length > 60 ? "Keep the name under 60 characters." : string.Empty;
            string eEmail = email != string.Empty && !IsEmail(email) ? "Enter a valid email or leave it blank." : string.Empty;
            response.SetElementContents("us-e-u-name", eName);
            response.SetElementContents("us-e-u-email", eEmail);
            response.SetElementContents("us-e-u-login", string.Empty);
            response.SetElementContents("us-e-u-pass", string.Empty);
            if (eName + eEmail != string.Empty)
            {
                return response;
            }
            string done;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff(c.Sbx);
                Staff? u = id != string.Empty ? staff.FirstOrDefault(x => x.Id == id) : null;
                if (id != string.Empty && u == null)
                {
                    response.SetElementContents("us-e-u-form", "That user no longer exists.");
                    return response;
                }
                if (u == null)
                {
                    if (!Regex.IsMatch(login, "^[a-z0-9._-]{3,30}$"))
                    {
                        response.SetElementContents("us-e-u-login", "3 to 30 letters, numbers, dots or dashes.");
                        return response;
                    }
                    if (staff.Any(x => x.Login == login || x.Id == login))
                    {
                        response.SetElementContents("us-e-u-login", "That user name is taken.");
                        return response;
                    }
                    if (pass.Length < 8)
                    {
                        response.SetElementContents("us-e-u-pass", "Use at least 8 characters.");
                        return response;
                    }
                    if (staff.Count >= 200)
                    {
                        response.SetElementContents("us-e-u-form", "The newsroom is full (200 users).");
                        return response;
                    }
                    u = new Staff { Id = login, Login = login };
                    staff.Add(u);
                    done = "user-add";
                }
                else
                {
                    if (pass != string.Empty && pass.Length < 8)
                    {
                        response.SetElementContents("us-e-u-pass", "Use at least 8 characters.");
                        return response;
                    }
                    done = "user";
                }
                bool self = u.Id == c.U.Id;
                u.Name = name;
                u.Title = title;
                u.Dept = dept;
                u.Email = email;
                u.Bio = bio;
                u.FrontPage = GetDataValue("ufront") == "1";
                if (!self)
                {
                    u.Admin = GetDataValue("uadmin") == "1";
                    u.Active = GetDataValue("uactive") == "1";
                }
                u.Perms = u.Admin ? new Dictionary<string, string>() : perms;
                if (pass != string.Empty)
                {
                    u.Hash = Access.HashPassword(pass);
                    u.Demo = false;
                }
                await Store.SaveStaff(staff, c.Sbx);
                if (pass != string.Empty || !u.Active)
                {
                    await Auth.CloseUser(c.Sbx, u.Id, Store.CleanId(GetDataValue("tok")));
                }
                id = u.Id;
            }
            await Store.Log(c.Sbx, c.U.Id, done, id, true);
            response.ExecuteScript("AdminUsersJs.saved();AdminUsersJs.toast('User saved');");
            return response;
        }
    }
}
