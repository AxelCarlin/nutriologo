namespace Nutriologo.Application.Contracts;

public class AiGuardException : Exception
{
    public AiGuardException(string message) : base(message) { }
}
