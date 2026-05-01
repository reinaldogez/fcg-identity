namespace FCG.Domain.Exceptions;

public class DomainConflictException : DomainException
{
    public DomainConflictException(string mensagem)
        : base(mensagem) { }
}
