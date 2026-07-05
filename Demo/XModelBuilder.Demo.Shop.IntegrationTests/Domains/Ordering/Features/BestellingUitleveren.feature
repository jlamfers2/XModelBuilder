#language: nl
Functionaliteit: Bestelling uitleveren
    Als magazijnmedewerker wil ik betaalde bestellingen verzenden
    en mogen onbetaalde bestellingen of klanten dat niet doen.

Scenario: Magazijn verzendt een betaalde bestelling
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    En ik een betaalde bestelling heb geplaatst voor 1 x "SKU-PHONE-1"
    Als ik ben ingelogd als magazijnmedewerker "wendy@shop.test"
    En ik de bestelling verzend
    Dan wordt de verzending geaccepteerd
    En heeft de bestelling status "Shipped"

Scenario: Een onbetaalde bestelling kan niet verzonden worden
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    En ik een bestelling heb geplaatst voor 1 x "SKU-PHONE-1"
    Als ik ben ingelogd als magazijnmedewerker "wendy@shop.test"
    En ik de bestelling verzend
    Dan wordt de verzending geweigerd

Scenario: Een klant mag geen bestellingen verzenden
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    En ik een betaalde bestelling heb geplaatst voor 1 x "SKU-PHONE-1"
    Als ik de bestelling verzend
    Dan word ik afgewezen als verboden
