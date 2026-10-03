namespace Nutriologo.Application.Common;

public interface IUseCase<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
