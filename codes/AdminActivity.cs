using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminActivity : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Activity");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--lv-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("lv-menu", Auth.Menu("lv", "AdminActivity", c, site));
            response.SetElementContents("lv-who", Auth.Who("lv", c));
            response.SetElementContents("lv-demo", Auth.DemoPill("lv", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminActivityJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminActivityJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"lv-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "lv-p" : "lv-y") : string.Empty) + "\">" + ch + "</i>");
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
                response.ExecuteScript("AdminActivityJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            StringBuilder sb = new StringBuilder("<div class=\"lv-ptop\"><div><h1>Activity</h1><p>" + (c.U.Admin ? "Everything the newsroom does, including actions the server refused." : "Your own actions, including any the server refused.") + "</p></div></div><div class=\"lv-lf\">");
            sb.Append("<div class=\"lv-range\" role=\"group\" aria-label=\"Period\"><button type=\"button\" class=\"lv-chip\" data-r=\"today\" onclick=\"AdminActivityJs.setRange('today')\">Today</button><button type=\"button\" class=\"lv-chip lv-act\" data-r=\"7\" onclick=\"AdminActivityJs.setRange('7')\">7 days</button><button type=\"button\" class=\"lv-chip\" data-r=\"30\" onclick=\"AdminActivityJs.setRange('30')\">30 days</button><button type=\"button\" class=\"lv-chip\" data-r=\"all\" onclick=\"AdminActivityJs.setRange('all')\">All</button></div>");
            sb.Append("<div class=\"lv-dates\" style=\"display:flex;gap:10px\"><label for=\"lv-from\">From<input type=\"date\" id=\"lv-from\" onchange=\"AdminActivityJs.custom()\"></label><label for=\"lv-to\">To<input type=\"date\" id=\"lv-to\" onchange=\"AdminActivityJs.custom()\"></label></div>");
            if (c.U.Admin)
            {
                sb.Append("<label for=\"lv-whoq\">Person<input type=\"search\" class=\"lv-who-in\" id=\"lv-whoq\" list=\"lv-people\" maxlength=\"60\" placeholder=\"Everyone\" autocomplete=\"off\" oninput=\"AdminActivityJs.who()\"></label><datalist id=\"lv-people\">");
                foreach (Staff u in staff.Values.OrderBy(x => x.Name))
                {
                    sb.Append("<option value=\"" + Ui.Attr(u.Name) + "\" label=\"" + Ui.Attr(u.Login + " · " + u.Title) + "\"></option>");
                }
                sb.Append("</datalist>");
            }
            sb.Append("<label for=\"lv-aq\">Search<input type=\"search\" class=\"lv-q-in\" id=\"lv-aq\" maxlength=\"60\" placeholder=\"Action or item\" autocomplete=\"off\" oninput=\"AdminActivityJs.who()\"></label>");
            sb.Append("<label for=\"lv-okf\">Outcome<select id=\"lv-okf\" onchange=\"AdminActivityJs.list(1)\"><option value=\"\">All outcomes</option><option value=\"no\">Refused only</option></select></label></div>");
            sb.Append("<div class=\"lv-log\"><div class=\"lv-lr lv-lh\"><span>When</span><span>Who</span><span>Action</span><span>Item</span><span>Result</span></div><div id=\"lv-loglist\"></div></div><div id=\"lv-logpager\"></div>");
            response.SetElementContents("lv-main", sb.ToString());
            response.ExecuteScript("AdminActivityJs.loaded();");
            return response;
        }

        private const int PageSize = 50;

        public async Task<ApiResponse> Filter()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
                return response;
            }
            List<Activity> acts = await Store.LoadActivity(c.Sbx);
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            string q = c.U.Admin ? Ui.Clip(GetDataValue("who"), 60).Trim() : string.Empty;
            IEnumerable<Activity> found = acts;
            if (!c.U.Admin)
            {
                found = found.Where(a => a.User == c.U.Id);
            }
            else if (q != string.Empty)
            {
                HashSet<string> ids = new HashSet<string>(staff.Values.Where(u => u.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || u.Login.Contains(q, StringComparison.OrdinalIgnoreCase)).Select(u => u.Id));
                found = found.Where(a => ids.Contains(a.User) || a.User.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            string aq = Ui.Clip(GetDataValue("aq"), 60).Trim();
            if (aq != string.Empty)
            {
                found = found.Where(a => a.Action.Contains(aq, StringComparison.OrdinalIgnoreCase) || a.Target.Contains(aq, StringComparison.OrdinalIgnoreCase));
            }
            if (GetDataValue("ok") == "no")
            {
                found = found.Where(a => !a.Ok);
            }
            if (DateTime.TryParse(GetDataValue("dfrom"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime from))
            {
                found = found.Where(a => a.When >= from);
            }
            if (DateTime.TryParse(GetDataValue("dto"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime to))
            {
                found = found.Where(a => a.When < to);
            }
            List<Activity> all = found.OrderByDescending(a => a.When).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), PageSize, all.Count);
            StringBuilder sb = new StringBuilder();
            if (all.Count == 0)
            {
                sb.Append("<div class=\"lv-lr\"><span class=\"lv-none\">Nothing matches in this period.</span></div>");
            }
            foreach (Activity a in all.Skip((page - 1) * PageSize).Take(PageSize))
            {
                string name = staff.TryGetValue(a.User, out Staff? u) ? u.Name : a.User;
                sb.Append("<div class=\"lv-lr" + (a.Ok ? string.Empty : " lv-bad") + "\"><span class=\"lv-c-t\">" + Ui.Time(a.When, "day-hm") + "</span><span><b>" + Ui.Enc(name) + "</b></span><span>" + Ui.Enc(a.Action) + "</span><span title=\"" + Ui.Attr(a.Target) + "\">" + Ui.Enc(a.Target) + "</span><span>" + (a.Ok ? "<span class=\"lv-yes\">Done</span>" : "<span class=\"lv-no\">Refused</span>") + "</span></div>");
            }
            response.SetElementContents("lv-loglist", sb.ToString());
            response.SetElementContents("lv-logpager", Ui.Pager("lv", page, PageSize, all.Count, "entry", "entries", "AdminActivityJs.list"));
            response.ExecuteScript("AdminActivityJs.listed();");
            return response;
        }
    }
}
