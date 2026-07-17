using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels
{
    public class SidebarPageVM
    {
        public int PageId { get; set; }

    public string DisplayName { get; set; }

    public string Controller { get; set; }

    public string Action { get; set; }

    public string Icon { get; set; }

    public int ParentPageId { get; set; }

    public int DisplayOrder { get; set; }
    }
}