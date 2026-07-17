using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels
{
    public class PageVM
    {
        public int PageId { get; set; }

    public string PageName { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public int? ParentPageId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsMenu { get; set; }

    public bool IsActive { get; set; }
    }
}