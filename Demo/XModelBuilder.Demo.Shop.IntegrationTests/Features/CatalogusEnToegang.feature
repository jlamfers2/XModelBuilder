#language: nl
Functionaliteit: Catalogus en toegang
    Alleen een beheerder mag de catalogus uitbreiden,
    en een klant mag uitsluitend zijn eigen bestellingen inzien.

Scenario: Beheerder voegt een product toe
    Gegeven ik ben ingelogd als beheerder "admin@shop.test"
    Als ik het volgende product toevoeg:
        | Veld          | Waarde       |
        | Sku           | SKU-LAPTOP-9 |
        | Name          | Demo Laptop  |
        | UnitPrice     | 999.99       |
        | StockQuantity | 5            |
        | Category      | Laptops      |
    Dan wordt het product toegevoegd
    En bevat de catalogus een product met sku "SKU-LAPTOP-9"

Scenario: Een klant mag geen product toevoegen
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    Als ik het volgende product toevoeg:
        | Veld          | Waarde     |
        | Sku           | SKU-HACK-1 |
        | Name          | Verboden   |
        | UnitPrice     | 1.00       |
        | StockQuantity | 1          |
        | Category      | Books      |
    Dan word ik afgewezen als verboden

Scenario: Een klant ziet de bestellingen van een ander niet
    Gegeven ik ben ingelogd als klant "alice@shop.test"
    En ik een bestelling heb geplaatst voor 1 x "SKU-PHONE-1"
    Als ik ben ingelogd als klant "bob@shop.test"
    En ik de bestellingen opvraag van "alice@shop.test"
    Dan word ik afgewezen als verboden
