#language: nl
Functionaliteit: Bestelling plaatsen
    Als klant wil ik een bestelling kunnen plaatsen
    zodat ik producten uit de catalogus kan kopen.

Scenario: Klant plaatst een geldige bestelling
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    Als ik de volgende bestelling plaats:
        | Veld              | Waarde      |
        | Lines[0].Sku      | SKU-PHONE-1 |
        | Lines[0].Quantity | 2           |
    Dan wordt de bestelling aangemaakt
    En heeft de bestelling status "Pending"
    En is het totaalbedrag 1000.00

Scenario: Bestelling met onvoldoende voorraad wordt geweigerd
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    Als ik de volgende bestelling plaats:
        | Veld              | Waarde     |
        | Lines[0].Sku      | SKU-BOOK-1 |
        | Lines[0].Quantity | 5          |
    Dan wordt de bestelling geweigerd wegens onvoldoende voorraad

Scenario: Een gast mag geen bestelling plaatsen
    Gegeven ik ben niet ingelogd
    Als ik de volgende bestelling plaats:
        | Veld              | Waarde      |
        | Lines[0].Sku      | SKU-PHONE-1 |
        | Lines[0].Quantity | 1           |
    Dan word ik afgewezen als niet-geautoriseerd

Scenario: Bestelling met kortingscode verlaagt het totaal
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    Als ik de volgende bestelling plaats:
        | Veld              | Waarde      |
        | Lines[0].Sku      | SKU-PHONE-1 |
        | Lines[0].Quantity | 1           |
        | DiscountCode      | WELCOME10   |
    Dan wordt de bestelling aangemaakt
    En is het kortingsbedrag 50.00
    En is het totaalbedrag 450.00
