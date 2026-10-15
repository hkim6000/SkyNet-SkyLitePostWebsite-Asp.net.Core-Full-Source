using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminDesks : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Departments");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--dp-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("dp-menu", Auth.Menu("dp", "AdminDesks", c, site));
            response.SetElementContents("dp-who", Auth.Who("dp", c));
            response.SetElementContents("dp-demo", Auth.DemoPill("dp", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminDesksJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminDesksJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"dp-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "dp-p" : "dp-y") : string.Empty) + "\">" + ch + "</i>");
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
                response.ExecuteScript("AdminDesksJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (!c.U.Admin)
            {
                await Store.Log(c.Sbx, c.U.Id, "departments", "open", false);
                response.SetElementContents("dp-main", "<div class=\"dp-ptop\"><div><h1>Departments</h1></div></div><div class=\"dp-info-box\">Only the editor-in-chief can change departments and sections.</div>");
                return response;
            }
            response.SetElementContents("dp-main", MainHtml(site));
            response.ExecuteScript("AdminDesksJs.loaded();");
            return response;
        }

        private static string Count(int n)
        {
            return n + (n == 1 ? " story" : " stories");
        }

        private static string MainHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"dp-ptop\"><div><h1>Departments</h1><p>Name, order and sections of the paper&rsquo;s departments. The order here is the order in the menus and footer.</p></div></div>");
            sb.Append("<div class=\"dp-lbar\"><div class=\"dp-acts\"><span class=\"dp-hint\">A department or section with stories can&rsquo;t be deleted. Web addresses stay the same when you rename.</span><span class=\"dp-dirty\" id=\"dp-dirty\"></span></div><div class=\"dp-acts\"><button type=\"button\" class=\"dp-btn dp-btn-sm dp-btn-accent\" onclick=\"AdminDesksJs.save()\">Save departments</button></div></div><div class=\"dp-desks\" id=\"dp-desks\">");
            foreach (Desk d in site.Desks)
            {
                int dn = site.Stories.Count(s => d.Sections.Any(x => x.Key == s.Section));
                sb.Append("<div class=\"dp-dk\" data-key=\"" + Ui.Attr(d.Key) + "\"><div class=\"dp-dtool\"><button type=\"button\" class=\"dp-ib dp-grip\" aria-label=\"Drag to reorder\" title=\"Drag to reorder\" onpointerdown=\"AdminDesksJs.drag(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button><b>" + Ui.Enc(d.Name) + "</b><span class=\"dp-key dp-dkey\">?d=" + Ui.Enc(d.Key) + "</span><span class=\"dp-cnt2\">" + Count(dn) + "</span><button type=\"button\" class=\"dp-ib\" aria-label=\"Move up\" title=\"Move up\" onclick=\"AdminDesksJs.move(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" class=\"dp-ib\" aria-label=\"Move down\" title=\"Move down\" onclick=\"AdminDesksJs.move(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button><button type=\"button\" class=\"dp-ib dp-del\" aria-label=\"Delete department\" title=\"" + (dn > 0 ? "Has " + Count(dn) + " – move or delete them first" : "Delete department") + "\" onclick=\"AdminDesksJs.remove(this)\"" + (dn > 0 ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button></div>");
                sb.Append("<div class=\"dp-dbody\"><div class=\"dp-g2\"><label class=\"dp-field\" for=\"dp-d-" + Ui.Attr(d.Key) + "\"><span>Name</span><input type=\"text\" class=\"dp-dname\" id=\"dp-d-" + Ui.Attr(d.Key) + "\" maxlength=\"30\" value=\"" + Ui.Attr(d.Name) + "\"></label><label class=\"dp-field\" for=\"dp-b-" + Ui.Attr(d.Key) + "\"><span>Short description (shown on the department page)</span><input type=\"text\" class=\"dp-dblurb\" id=\"dp-b-" + Ui.Attr(d.Key) + "\" maxlength=\"140\" value=\"" + Ui.Attr(d.Blurb) + "\"></label></div><div class=\"dp-secs\">");
                foreach (DeskSection s in d.Sections)
                {
                    int sn = site.Stories.Count(x => x.Section == s.Key);
                    sb.Append("<div class=\"dp-sr2\" data-key=\"" + Ui.Attr(s.Key) + "\"><button type=\"button\" class=\"dp-ib dp-grip\" aria-label=\"Drag to reorder\" title=\"Drag to reorder\" onpointerdown=\"AdminDesksJs.drag(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button><input type=\"text\" maxlength=\"30\" aria-label=\"Section name\" id=\"dp-s-" + Ui.Attr(s.Key) + "\" value=\"" + Ui.Attr(s.Name) + "\"><span class=\"dp-key\">?s=" + Ui.Enc(s.Key) + "</span><span class=\"dp-cnt2\">" + Count(sn) + "</span><button type=\"button\" class=\"dp-ib dp-up2\" aria-label=\"Move up\" title=\"Move up\" onclick=\"AdminDesksJs.move(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" class=\"dp-ib dp-del\" aria-label=\"Delete section\" title=\"" + (sn > 0 ? "Has " + Count(sn) + " – move or delete them first" : "Delete section") + "\" onclick=\"AdminDesksJs.remove(this)\"" + (sn > 0 ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button></div>");
                }
                sb.Append("</div><button type=\"button\" class=\"dp-addsec\" onclick=\"AdminDesksJs.addSection(this)\">+ Add section</button></div></div>");
            }
            sb.Append("</div><div class=\"dp-add\"><button type=\"button\" onclick=\"AdminDesksJs.addDesk()\">+ Add a department</button></div>");
            sb.Append("<script type=\"application/json\" id=\"dp-icons\">{\"drag\":" + System.Text.Json.JsonSerializer.Serialize("<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg>") + ",\"up\":" + System.Text.Json.JsonSerializer.Serialize("<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg>") + ",\"down\":" + System.Text.Json.JsonSerializer.Serialize("<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg>") + ",\"trash\":" + System.Text.Json.JsonSerializer.Serialize("<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg>") + "}</script>");
            return sb.ToString();
        }

        private sealed class SecIn
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed class DeskIn
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Blurb { get; set; } = string.Empty;
            public List<SecIn> Sections { get; set; } = new List<SecIn>();
        }

        private static string NewKey(string name, HashSet<string> taken)
        {
            string b = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (b.Length > 30)
            {
                b = b.Substring(0, 30).Trim('-');
            }
            if (b == string.Empty || b == "home" || b == "all" || b == "front")
            {
                b = "desk" + (b == string.Empty ? string.Empty : "-" + b);
            }
            string k = b;
            int n = 2;
            while (taken.Contains(k))
            {
                k = b + "-" + n++;
            }
            taken.Add(k);
            return k;
        }

        private static string Problem(List<DeskIn> input, SiteData site, out int di, out int si)
        {
            di = -1;
            si = -1;
            if (input.Count == 0)
            {
                return "Keep at least one department.";
            }
            if (input.Count > 20)
            {
                return "Up to 20 departments.";
            }
            Dictionary<string, string> owner = new Dictionary<string, string>();
            foreach (Desk d in site.Desks)
            {
                foreach (DeskSection s in d.Sections)
                {
                    owner[s.Key] = d.Key;
                }
            }
            HashSet<string> seenD = new HashSet<string>();
            HashSet<string> seenS = new HashSet<string>();
            for (int i = 0; i < input.Count; i++)
            {
                DeskIn d = input[i];
                string name = (d.Name ?? string.Empty).Trim();
                if (name.Length < 2 || name.Length > 30)
                {
                    di = i;
                    return "Each department needs a name (2 to 30 characters).";
                }
                if (input.Take(i).Any(x => (x.Name ?? string.Empty).Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    di = i;
                    return "Two departments are called “" + name + "”.";
                }
                if (d.Key != string.Empty && (!site.Desks.Any(x => x.Key == d.Key) || !seenD.Add(d.Key)))
                {
                    di = i;
                    return "Please reload the page and try again.";
                }
                if (d.Sections.Count == 0)
                {
                    di = i;
                    return "“" + name + "” needs at least one section.";
                }
                if (d.Sections.Count > 20)
                {
                    di = i;
                    return "Up to 20 sections per department.";
                }
                for (int j = 0; j < d.Sections.Count; j++)
                {
                    SecIn s = d.Sections[j];
                    string sn = (s.Name ?? string.Empty).Trim();
                    if (sn.Length < 2 || sn.Length > 30)
                    {
                        di = i;
                        si = j;
                        return "Each section needs a name (2 to 30 characters).";
                    }
                    if (d.Sections.Take(j).Any(x => (x.Name ?? string.Empty).Trim().Equals(sn, StringComparison.OrdinalIgnoreCase)))
                    {
                        di = i;
                        si = j;
                        return "“" + name + "” has two sections called “" + sn + "”.";
                    }
                    if (s.Key != string.Empty)
                    {
                        if (!owner.TryGetValue(s.Key, out string? was) || !seenS.Add(s.Key))
                        {
                            return "Please reload the page and try again.";
                        }
                        if (was != d.Key)
                        {
                            di = i;
                            si = j;
                            return "Sections can't move to another department.";
                        }
                    }
                }
            }
            return string.Empty;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminDesksJs.expired();");
                return response;
            }
            if (!c.U.Admin)
            {
                return await Refuse(response, c, "departments", "save", "Only the editor-in-chief can change departments.");
            }
            List<DeskIn>? input;
            try
            {
                input = System.Text.Json.JsonSerializer.Deserialize<List<DeskIn>>(GetDataValue("desks"), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException)
            {
                input = null;
            }
            if (input == null)
            {
                response.ExecuteScript("AdminDesksJs.toast('Could not read the departments.');");
                return response;
            }
            foreach (DeskIn d in input)
            {
                d.Key ??= string.Empty;
                d.Sections ??= new List<SecIn>();
                foreach (SecIn s in d.Sections)
                {
                    s.Key ??= string.Empty;
                }
            }
            string blocked = string.Empty;
            List<string> removedDesks = new List<string>();
            List<string> removedSecs = new List<string>();
            List<string> addedDesks = new List<string>();
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                string problem = Problem(input, site, out int di, out int si);
                if (problem != string.Empty)
                {
                    response.ExecuteScript("AdminDesksJs.flag(" + di + "," + si + ");AdminDesksJs.toast('" + Ui.Js(problem) + "');");
                    return response;
                }
                HashSet<string> keepD = new HashSet<string>(input.Where(d => d.Key != string.Empty).Select(d => d.Key));
                HashSet<string> keepS = new HashSet<string>(input.SelectMany(d => d.Sections).Where(s => s.Key != string.Empty).Select(s => s.Key));
                foreach (Desk d in site.Desks)
                {
                    foreach (DeskSection s in d.Sections.Where(s => !keepS.Contains(s.Key)))
                    {
                        int n = site.Stories.Count(x => x.Section == s.Key);
                        if (n > 0)
                        {
                            blocked = (keepD.Contains(d.Key) ? "The section " : "The department " + d.Name + " has stories: ") + "“" + s.Name + "” has " + Count(n) + ". Move or delete them first.";
                            break;
                        }
                        removedSecs.Add(s.Key);
                    }
                    if (blocked != string.Empty)
                    {
                        break;
                    }
                    if (!keepD.Contains(d.Key))
                    {
                        removedDesks.Add(d.Key);
                    }
                }
                if (blocked == string.Empty)
                {
                    HashSet<string> deskKeys = new HashSet<string>(site.Desks.Select(d => d.Key)) { "home" };
                    HashSet<string> secKeys = new HashSet<string>(site.Desks.SelectMany(d => d.Sections).Select(s => s.Key));
                    List<Desk> next = new List<Desk>();
                    foreach (DeskIn d in input)
                    {
                        string key = d.Key != string.Empty ? d.Key : NewKey(d.Name.Trim(), deskKeys);
                        if (d.Key == string.Empty)
                        {
                            addedDesks.Add(key);
                        }
                        Desk nd = new Desk { Key = key, Name = d.Name.Trim(), Blurb = Ui.Clip(d.Blurb, 140) };
                        foreach (SecIn s in d.Sections)
                        {
                            nd.Sections.Add(new DeskSection { Key = s.Key != string.Empty ? s.Key : NewKey(s.Name.Trim(), secKeys), Name = s.Name.Trim() });
                        }
                        next.Add(nd);
                    }
                    site.Desks = next;
                    await Store.SaveSite(site, c.Sbx);
                    if (removedDesks.Count + removedSecs.Count + addedDesks.Count > 0)
                    {
                        Dictionary<string, List<PageModule>> pages = await Store.LoadPages(c.Sbx);
                        foreach (string k in removedDesks)
                        {
                            pages.Remove(k);
                        }
                        foreach (List<PageModule> list in pages.Values)
                        {
                            list.RemoveAll(m => removedDesks.Any(k => m.Source == "d:" + k) || removedSecs.Any(k => m.Source == "s:" + k));
                        }
                        foreach (string k in addedDesks)
                        {
                            pages[k] = new List<PageModule>
                            {
                                new PageModule { Id = "m" + Guid.NewGuid().ToString("N").Substring(0, 10), Style = "lead", Source = "d:" + k, Count = 4 },
                                new PageModule { Id = "m" + Guid.NewGuid().ToString("N").Substring(0, 10), Style = "rundown", Title = "Latest", Source = "d:" + k, Count = 8 },
                            };
                        }
                        await Store.SavePages(pages, c.Sbx);
                        List<Staff> staff = await Store.LoadStaff(c.Sbx);
                        foreach (Staff u in staff)
                        {
                            foreach (string k in removedSecs)
                            {
                                u.Perms.Remove(k);
                            }
                            if (!next.Any(d => d.Key == u.Dept))
                            {
                                u.Dept = next[0].Key;
                            }
                        }
                        await Store.SaveStaff(staff, c.Sbx);
                    }
                }
            }
            if (blocked != string.Empty)
            {
                return await Refuse(response, c, "departments", "delete", blocked);
            }
            string what = (addedDesks.Count > 0 ? "added " + string.Join(", ", addedDesks) + "; " : string.Empty) + (removedDesks.Count > 0 ? "removed " + string.Join(", ", removedDesks) + "; " : string.Empty) + (removedSecs.Count > 0 ? "removed sections " + string.Join(", ", removedSecs) + "; " : string.Empty);
            await Store.Log(c.Sbx, c.U.Id, "departments", what == string.Empty ? "save" : what.TrimEnd(' ', ';'), true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Chrome(response, c, fresh);
            response.SetElementContents("dp-main", MainHtml(fresh));
            response.ExecuteScript("AdminDesksJs.loaded();AdminDesksJs.toast('Departments saved');");
            return response;
        }
    }
}
