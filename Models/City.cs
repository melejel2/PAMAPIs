﻿using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class City
{
    public int CityId { get; set; }

    public string? CityName { get; set; }

    public int CountryId { get; set; }
}
