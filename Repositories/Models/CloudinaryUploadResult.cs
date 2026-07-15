using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models
{
    public class CloudinaryUploadResult
{
    public string PublicId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
}