namespace eShop.Ordering.API.Application.Commands;

/// <summary>Marker interface used to unwrap the business command inside an <see cref="IdentifiedCommand{T, R}"/>.</summary>
public interface IIdentifiedCommand
{
    object Command { get; }
}

public class IdentifiedCommand<T, R> : IRequest<R>, IIdentifiedCommand
    where T : IRequest<R>
{
    public T Command { get; }
    public Guid Id { get; }

    object IIdentifiedCommand.Command => Command;

    public IdentifiedCommand(T command, Guid id)
    {
        Command = command;
        Id = id;
    }
}
