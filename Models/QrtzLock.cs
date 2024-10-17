using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class QrtzLock
{
    public string SchedName { get; set; } = null!;

    public string LockName { get; set; } = null!;
}
