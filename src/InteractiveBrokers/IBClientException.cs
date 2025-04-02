namespace InteractiveBrokers;

public class IBClientException : Exception
{
    public IBClientException() { 
    }

    public IBClientException(string message) : base(message) { 
    }
    
    public IBClientException(string message, Exception innerException) : base(message, innerException) { 
    }
}
