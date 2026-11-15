namespace Mymo.Chat.Bll;

public interface IChatRealtime
{
    Task ReceiveMessage(Guid conversationId, object dto);
    Task MessageDeleted(Guid conversationId, object dto);
}
