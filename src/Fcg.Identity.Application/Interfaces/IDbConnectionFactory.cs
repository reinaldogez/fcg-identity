using System.Data;

namespace Fcg.Identity.Application.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateOpenConnection();
}
