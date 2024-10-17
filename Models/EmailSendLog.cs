using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class EmailSendLog
{
    public int Id { get; set; }

    public string? EmailType { get; set; }

    public int CountryId { get; set; }

    public DateTime? SentAt { get; set; }
}
