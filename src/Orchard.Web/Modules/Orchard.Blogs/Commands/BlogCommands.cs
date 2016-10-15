using System;
using System.Linq;
using System.Xml.Linq;
using Orchard.Blogs.Models;
using Orchard.Commands;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Aspects;
using Orchard.ContentPicker.Models;
using Orchard.Core.Common.Models;
using Orchard.Core.Navigation.Models;
using Orchard.Security;
using Orchard.Blogs.Services;
using Orchard.Core.Navigation.Services;
using Orchard.Settings;
using Orchard.Core.Title.Models;
using Orchard.UI.Navigation;

namespace Orchard.Blogs.Commands {
    public class BlogCommands : DefaultOrchardCommandHandler {
        private readonly IContentManager _contentManager;
        private readonly IMembershipService _membershipService;
        private readonly IBlogService _blogService;
        private readonly IMenuService _menuService;
        private readonly ISiteService _siteService;
        private readonly INavigationManager _navigationManager;
        private readonly IArchiveService _archiveService;

        public BlogCommands(
            IContentManager contentManager,
            IMembershipService membershipService,
            IBlogService blogService,
            IMenuService menuService,
            ISiteService siteService,
            INavigationManager navigationManager,
            IArchiveService archiveService) {
            _contentManager = contentManager;
            _membershipService = membershipService;
            _blogService = blogService;
            _menuService = menuService;
            _siteService = siteService;
            _navigationManager = navigationManager;
            _archiveService = archiveService;
        }

        [OrchardSwitch]
        public string FeedUrl { get; set; }

        [OrchardSwitch]
        public int BlogId { get; set; }

        [OrchardSwitch]
        public string Owner { get; set; }

        [OrchardSwitch]
        public string Slug { get; set; }

        [OrchardSwitch]
        public string Title { get; set; }

        [OrchardSwitch]
        public string Description { get; set; }

        [OrchardSwitch]
        public string MenuText { get; set; }

        [OrchardSwitch]
        public string MenuName { get; set; }

        [OrchardSwitch]
        public bool Homepage { get; set; }

        [OrchardSwitch]
        public bool CreateWelcomePost { get; set; }

        [CommandName("blog create")]
        [CommandHelp("blog create [/Slug:<slug>] /Title:<title> [/Owner:<username>] [/Description:<description>] [/MenuName:<name>] [/MenuText:<menu text>] [/Homepage:true|false] [/CreateWelcomePost:true|false]\r\n\t" + "Creates a new Blog")]
        [OrchardSwitches("Slug,Title,Owner,Description,MenuText,Homepage,MenuName,CreateWelcomePost")]
        public void Create() {
            if (String.IsNullOrEmpty(Owner)) {
                Owner = _siteService.GetSiteSettings().SuperUser;
            }
            var owner = _membershipService.GetUser(Owner);

            if (owner == null) {
                Context.Output.WriteLine(T("Invalid username: {0}", Owner));
                return;
            }

            var blog = _contentManager.New("Blog");
            blog.As<ICommonPart>().Owner = owner;
            blog.As<TitlePart>().Title = Title;
            if (!String.IsNullOrEmpty(Description)) {
                blog.As<BlogPart>().Description = Description;
            }

            if (Homepage || !String.IsNullOrWhiteSpace(Slug)) {
                dynamic dblog = blog;
                if (dblog.AutoroutePart != null) {
                    dblog.AutoroutePart.UseCustomPattern = true;
                    dblog.AutoroutePart.CustomPattern = Homepage ? "/" : Slug;
                }
            }
            
            _contentManager.Create(blog);

            if (!String.IsNullOrWhiteSpace(MenuText)) {
                var menu = _menuService.GetMenu(MenuName);

                if (menu != null) {
                    var menuItem = _contentManager.Create<ContentMenuItemPart>("ContentMenuItem");
                    menuItem.Content = blog;
                    menuItem.As<MenuPart>().MenuPosition = _navigationManager.GetNextPosition(menu);
                    menuItem.As<MenuPart>().MenuText = MenuText;
                    menuItem.As<MenuPart>().Menu = menu;
                }
            }

            if(CreateWelcomePost) {
                var blogPost = _contentManager.New<BlogPostPart>("BlogPost");

                var text = T(
                    @"<p>You've successfully setup your Orchard Site and this is the homepage of your new site.
Here are a few things you can look at to get familiar with the application.
Once you feel confident you don't need this anymore, you can
<a href=""Admin/Contents/Edit/{0}"">remove it by going into editing mode</a>
and replacing it with whatever you want.</p>
<p>First things first - You'll probably want to <a href=""Admin/Settings"">manage your settings</a>
and configure Orchard to your liking. After that, you can head over to
<a href=""Admin/Themes"">manage themes to change or install new themes</a>
and really make it your own. Once you're happy with a look and feel, it's time for some content.
You can start creating new custom content types or start from the built-in ones by
<a href=""Admin/Contents/Create/Page"">adding a page</a>, or <a href=""Admin/Navigation"">managing your menus.</a></p>
<p>Finally, Orchard has been designed to be extended. It comes with a few built-in
modules such as pages and blogs or themes. If you're looking to add additional functionality,
you can do so by creating your own module or by installing one that somebody else built.
Modules are created by other users of Orchard just like you so if you feel up to it,
<a href=""http://orchardproject.net/contribution"">please consider participating</a>.</p>
<p>Thanks for using Orchard – The Orchard Team </p>", blogPost.Id).Text;

                blogPost.BlogPart = blog.As<BlogPart>();
                blogPost.As<TitlePart>().Title = T("Welcome to Orchard!").Text;
                blogPost.As<Autoroute.Models.AutoroutePart>().DisplayAlias = "welcome-to-orchard";
                blogPost.As<BodyPart>().Text = text;
                blogPost.Creator = owner;

                _contentManager.Create(blogPost, VersionOptions.Published);

                // blog
                // title
                // autoroute
                // body
                // comments
                // common
            }

            Context.Output.WriteLine(T("Blog created successfully"));
        }

        [CommandName("blog import")]
        [CommandHelp("blog import /BlogId:<id> /FeedUrl:<feed url> /Owner:<username>\r\n\t" + "Import all items from <feed url> into the blog specified by <id>")]
        [OrchardSwitches("FeedUrl,BlogId,Owner")]
        public void Import() {
            var owner = _membershipService.GetUser(Owner);

            if(owner == null) {
                Context.Output.WriteLine(T("Invalid username: {0}", Owner));
                return;
            }

            XDocument doc;

            try {
                Context.Output.WriteLine(T("Loading feed..."));
                doc = XDocument.Load(FeedUrl);
                Context.Output.WriteLine(T("Found {0} items", doc.Descendants("item").Count()));
            }
            catch (Exception ex) {
                throw new OrchardException(T("An error occurred while loading the feed at {0}.", FeedUrl), ex);
            }

            var blog = _blogService.Get(BlogId, VersionOptions.Latest);

            if ( blog == null ) {
                Context.Output.WriteLine(T("Blog not found with specified Id: {0}", BlogId));
                return;
            }

            foreach ( var item in doc.Descendants("item") ) {
                if (item != null) {
                    var postName = item.Element("title").Value;

                    Context.Output.WriteLine(T("Adding post: {0}...", postName.Substring(0, Math.Min(postName.Length, 40))));
                    var post = _contentManager.New("BlogPost");
                    post.As<ICommonPart>().Owner = owner;
                    post.As<ICommonPart>().Container = blog;
                    post.As<TitlePart>().Title = postName;
                    post.As<BodyPart>().Text = item.Element("description").Value;
                    _contentManager.Create(post);
                }
            }

            Context.Output.WriteLine(T("Import feed completed."));
        }

        [CommandName("blog build archive")]
        [CommandHelp("blog build archive /BlogId:<id> \r\n\t" + "Rebuild the archive information for the blog specified by <id>")]
        [OrchardSwitches("BlogId")]
        public void BuildArchive() {

            var blog = _blogService.Get(BlogId, VersionOptions.Latest);

            if (blog == null) {
                Context.Output.WriteLine(T("Blog not found with specified Id: {0}", BlogId));
                return;
            }

            _archiveService.RebuildArchive(blog.As<BlogPart>());
        }
    }
}