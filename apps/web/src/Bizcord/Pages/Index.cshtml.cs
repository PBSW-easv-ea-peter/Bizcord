using Bizcord.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bizcord.Pages
{
    // Welcome dashboard (wireframe). The chat itself is on the Chat page.
    public class IndexModel : PageModel
    {
        private readonly CurrentUser _currentUser;

        public IndexModel(CurrentUser currentUser)
        {
            _currentUser = currentUser;
        }

        public string UserName => _currentUser.Name;

        public void OnGet()
        {
        }
    }
}
