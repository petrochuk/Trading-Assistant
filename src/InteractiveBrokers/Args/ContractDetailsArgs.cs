﻿using AppCore;

namespace InteractiveBrokers.Args;

public class ContractDetailsArgs : EventArgs
{
    public required Contract Contract { get; init; }
}
