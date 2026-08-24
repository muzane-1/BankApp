using Microsoft.AspNetCore.Components.Authorization;

namespace eShop.WebApp.Services;

public class TransferService(
    OrderingService orderingService,
    AuthenticationStateProvider authenticationStateProvider)
{
    public async Task SubmitTransferAsync(TransferInfo transfer)
    {
        if (transfer.RequestId == default)
        {
            transfer.RequestId = Guid.NewGuid();
        }

        var buyerId = await authenticationStateProvider.GetBuyerIdAsync()
            ?? throw new InvalidOperationException("User does not have a buyer ID");
        var userName = await authenticationStateProvider.GetUserNameAsync()
            ?? throw new InvalidOperationException("User does not have a user name");

        // The ordering backend books every payment as an order, so the transfer is
        // submitted as a single line item priced at the transfer amount. This keeps
        // the order total, payment authorization, and AML audit amounts aligned.
        var request = new CreateOrderRequest(
            UserId: buyerId,
            UserName: userName,
            City: transfer.City ?? "N/A",
            Street: transfer.Street ?? "N/A",
            State: transfer.State ?? "N/A",
            Country: transfer.Country ?? "N/A",
            ZipCode: transfer.ZipCode ?? "N/A",
            CardNumber: "1111222233334444",
            CardHolderName: "TESTUSER",
            CardExpiration: DateTime.UtcNow.AddYears(1),
            CardSecurityNumber: "111",
            CardTypeId: 1,
            Buyer: buyerId,
            Items:
            [
                new TransferOrderItem
                {
                    Id = transfer.RequestId.ToString("N"),
                    ProductId = 1,
                    ProductName = $"Funds transfer to {transfer.BeneficiaryName}",
                    UnitPrice = transfer.TransferAmount,
                    Quantity = 1,
                }
            ]);

        await orderingService.CreateOrder(request, transfer.RequestId);
    }
}

public record CreateOrderRequest(
    string UserId,
    string UserName,
    string City,
    string Street,
    string State,
    string Country,
    string ZipCode,
    string CardNumber,
    string CardHolderName,
    DateTime CardExpiration,
    string CardSecurityNumber,
    int CardTypeId,
    string Buyer,
    List<TransferOrderItem> Items);

public class TransferOrderItem
{
    public required string Id { get; init; }
    public int ProductId { get; init; }
    public required string ProductName { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
}
