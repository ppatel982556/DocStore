using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels.Workspace
{
    public class PermissionPropagationVM
{
    public bool ApplyToChildren { get; set; }

    public bool ApplyToFiles { get; set; }

    public bool SyncParents { get; set; }

    public bool SyncGroup { get; set; }
}
}