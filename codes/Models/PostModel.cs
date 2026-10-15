namespace SkyLitePost.Models
{
    public class SiteData
    {
        public Settings Settings { get; set; } = new Settings();
        public List<Desk> Desks { get; set; } = new List<Desk>();
        public List<Story> Stories { get; set; } = new List<Story>();
        public List<ReaderComment> Comments { get; set; } = new List<ReaderComment>();
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();
        public List<Subscriber> Subscribers { get; set; } = new List<Subscriber>();
        public DateTime Anchor { get; set; }
    }

    public class Settings
    {
        public string Name { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Accent { get; set; } = "#B4232A";
        public string Theme { get; set; } = "light";
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string About { get; set; } = string.Empty;
        public bool Comments { get; set; } = true;
        public List<SocialLink> Social { get; set; } = new List<SocialLink>();
    }

    public class SocialLink
    {
        public string Key { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Show { get; set; } = true;
    }

    public class Desk
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public List<DeskSection> Sections { get; set; } = new List<DeskSection>();
    }

    public class DeskSection
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class Story
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Dek { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string Image { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string Video { get; set; } = string.Empty;
        public string VideoPoster { get; set; } = string.Empty;
        public string VideoCaption { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Status { get; set; } = "draft";
        public DateTime Date { get; set; }
        public DateTime Updated { get; set; }
        public bool Breaking { get; set; }
        public bool Front { get; set; }
        public int Views { get; set; }
        public bool AllowComments { get; set; } = true;
    }

    public class ReaderComment
    {
        public string Id { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Status { get; set; } = "pending";
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Full { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Credit { get; set; } = string.Empty;
        public int W { get; set; }
        public int H { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Kind { get; set; } = "photo";
        public int Seconds { get; set; }
    }

    public class Subscriber
    {
        public string Email { get; set; } = string.Empty;
        public DateTime When { get; set; }
    }

    public class PageModule
    {
        public string Id { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = "all";
        public int Count { get; set; }
        public string Pin { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Dept { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool FrontPage { get; set; }
        public bool Active { get; set; } = true;
        public bool Demo { get; set; }
        public string Hash { get; set; } = string.Empty;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Activity
    {
        public DateTime When { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; }
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string Sbx { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public DateTime Exp { get; set; }
    }
}
