namespace Fcg.Identity.Domain.Exceptions;

public class DomainConflictException(string mensagem) : DomainException(mensagem) { }
