namespace FCG.Domain.Exceptions;

public class DomainAuthException : DomainException
{
    public DomainAuthException(string mensagem) : base(mensagem) { }
}
