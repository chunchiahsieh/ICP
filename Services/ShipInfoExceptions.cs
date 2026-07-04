namespace ICP.Services;

public class ShipInfoBusinessException : Exception
{
    public ShipInfoBusinessException(string message) : base(message)
    {
    }
}

public class ShipInfoConcurrencyException : ShipInfoBusinessException
{
    public ShipInfoConcurrencyException(string message) : base(message)
    {
    }
}

public class ShipInfoNotFoundException : ShipInfoBusinessException
{
    public ShipInfoNotFoundException(string message) : base(message)
    {
    }
}

public class ShipInfoForbiddenException : ShipInfoBusinessException
{
    public ShipInfoForbiddenException(string message) : base(message)
    {
    }
}
