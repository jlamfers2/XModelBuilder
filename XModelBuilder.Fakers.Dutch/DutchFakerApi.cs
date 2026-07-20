namespace XModelBuilder.Fakers.Dutch;

/// <summary>
/// The Dutch faker method surface: deterministic generators for Netherlands-specific identifiers and
/// contact data - BSN, RSIN, BTW, KvK, IBAN, BIC, EAN, postcode, kenteken, phone numbers, and more.
/// Wherever an identifier carries an official check (the "elfproef" for BSN/RSIN/bank accounts, the
/// ISO&#160;13616 mod-97 check for IBAN, the GS1 check digit for EAN barcodes), the generated value is
/// a VALID one for that check, so it is accepted by systems that validate the structure. Values are
/// fictitious and must only be used as test data. The generators are thin wrappers over the shared,
/// country-agnostic <see cref="Checksums"/> and <see cref="RandomExtensions"/> helpers in XModelBuilder.
///
/// <para>
/// This is the object exposed by the <see cref="DutchFaker"/> namespace member
/// <see cref="DutchFaker.NL"/>, so its methods are addressed as <c>nl.&lt;method&gt;()</c> from tokens
/// (e.g. <c>"nl.bsn()"</c>, <c>"nl.postcode()"</c>) and via <c>xprovider.NL()</c> from C#.
/// </para>
///
/// <para>
/// All randomness flows through the injected, seeded <see cref="Random"/>: build a fresh
/// ServiceProvider per test (each with the same seed) and every run reproduces. Separate providers get
/// separate seeded RNGs, so parallel tests stay isolated. The values depend on how many times the RNG
/// has been drawn before (call order), exactly like the RNG-based methods on XFaker.
/// </para>
/// </summary>
public class DutchFakerApi(Random random)
{
    // Letters allowed on modern Dutch number plates: vowels (A E I O U) are excluded so plates never
    // spell words, and C, Q, W, Y are avoided as well (never issued in the common sidecodes).
    private const string PlateLetters = "BDFGHJKLMNPRSTVXZ";

    // Realistic IBAN bank codes (the 4-letter segment after the check digits).
    private static readonly string[] BankCodes =
        ["INGB", "RABO", "ABNA", "TRIO", "SNSB", "ASNB", "KNAB", "BUNQ", "RBRB", "FVLB"];

    // Real BIC/SWIFT codes of Dutch banks.
    private static readonly string[] Bics =
        ["INGBNL2A", "RABONL2U", "ABNANL2A", "TRIONL2U", "SNSBNL2A", "ASNBNL2A",
         "KNABNL2H", "BUNQNL2A", "RBRBNL21", "FVLBNL22", "DEUTNL2A", "BKMGNL2A"];

    // Number-plate sidecodes in the '#'/'?' template grammar ('#' = digit, '?' = plate letter).
    private static readonly string[] KentekenPatterns =
        ["???-##-?", "??-###-?", "?-###-??", "##-???-#", "#-???-##", "##-??-##"];

    // Weights for the first 8 digits of a 9-digit "elfproef" number (the 9th digit is the check).
    private static readonly int[] ElfproefBodyWeights = [9, 8, 7, 6, 5, 4, 3, 2];

    // Landline area codes paired with the length of the subscriber number, so area + subscriber is
    // always 10 digits (3-digit area -> 7-digit subscriber, 4-digit area -> 6-digit subscriber).
    private static readonly (string Area, int SubscriberLength)[] AreaCodes =
    [
        ("010", 7), ("013", 7), ("015", 7), ("020", 7), ("023", 7), ("024", 7), ("026", 7),
        ("030", 7), ("033", 7), ("035", 7), ("036", 7), ("038", 7), ("040", 7), ("043", 7),
        ("045", 7), ("050", 7), ("053", 7), ("055", 7), ("058", 7), ("070", 7), ("071", 7),
        ("072", 7), ("073", 7), ("076", 7), ("078", 7), ("079", 7),
        ("0111", 6), ("0113", 6), ("0118", 6), ("0161", 6), ("0180", 6), ("0182", 6), ("0222", 6),
        ("0229", 6), ("0251", 6), ("0299", 6), ("0343", 6), ("0475", 6), ("0512", 6), ("0592", 6),
    ];

    private static readonly string[] Provincies =
    [
        "Groningen", "Friesland", "Drenthe", "Overijssel", "Flevoland", "Gelderland", "Utrecht",
        "Noord-Holland", "Zuid-Holland", "Zeeland", "Noord-Brabant", "Limburg",
    ];

    // All current Dutch municipalities (gemeenten), per the official CBS/Wikipedia list (peildatum
    // 1 January 2024; unchanged for 2025). The two municipalities both displayed as "Bergen"
    // (Noord-Holland and Limburg) are listed once. The Caribbean public bodies (Bonaire, Saba, Sint
    // Eustatius) are not gemeenten and are excluded. Non-ASCII letters use \u escapes so the source
    // compiles regardless of file encoding.
    private static readonly string[] Gemeenten =
    [
        "'s-Hertogenbosch", "Aa en Hunze", "Aalsmeer", "Aalten", "Achtkarspelen", "Alblasserdam",
        "Albrandswaard", "Alkmaar", "Almelo", "Almere", "Alphen aan den Rijn", "Alphen-Chaam",
        "Altena", "Ameland", "Amersfoort", "Amstelveen", "Amsterdam", "Apeldoorn",
        "Arnhem", "Assen", "Asten", "Baarle-Nassau", "Baarn", "Barendrecht",
        "Barneveld", "Beek", "Beekdaelen", "Beesel", "Berg en Dal", "Bergeijk",
        "Bergen", "Bergen op Zoom", "Berkelland", "Bernheze", "Best", "Beuningen",
        "Beverwijk", "Bladel", "Blaricum", "Bloemendaal", "Bodegraven-Reeuwijk", "Boekel",
        "Borger-Odoorn", "Borne", "Borsele", "Boxtel", "Breda", "Bronckhorst",
        "Brummen", "Brunssum", "Bunnik", "Bunschoten", "Buren", "Capelle aan den IJssel",
        "Castricum", "Coevorden", "Cranendonck", "Culemborg", "Dalfsen", "Dantumadeel",
        "De Bilt", "De Friese Meren", "De Ronde Venen", "De Wolden", "Delft", "Den Haag",
        "Den Helder", "Deurne", "Deventer", "Diemen", "Dijk en Waard", "Dinkelland",
        "Doesburg", "Doetinchem", "Dongen", "Dordrecht", "Drechterland", "Drimmelen",
        "Dronten", "Druten", "Duiven", "Echt-Susteren", "Edam-Volendam", "Ede",
        "Eemnes", "Eemsdelta", "Eersel", "Eijsden-Margraten", "Eindhoven", "Elburg",
        "Emmen", "Enkhuizen", "Enschede", "Epe", "Ermelo", "Etten-Leur",
        "Geertruidenberg", "Geldrop-Mierlo", "Gemert-Bakel", "Gennep", "Gilze en Rijen", "Goeree-Overflakkee",
        "Goes", "Goirle", "Gooise Meren", "Gorinchem", "Gouda", "Groningen",
        "Gulpen-Wittem", "Haaksbergen", "Haarlem", "Haarlemmermeer", "Halderberge", "Hardenberg",
        "Harderwijk", "Hardinxveld-Giessendam", "Harlingen", "Hattem", "Heemskerk", "Heemstede",
        "Heerde", "Heerenveen", "Heerlen", "Heeze-Leende", "Heiloo", "Hellendoorn",
        "Helmond", "Hendrik-Ido-Ambacht", "Hengelo", "Het Hogeland", "Heumen", "Heusden",
        "Hillegom", "Hilvarenbeek", "Hilversum", "Hoeksche Waard", "Hof van Twente", "Hollands Kroon",
        "Hoogeveen", "Hoorn", "Horst aan de Maas", "Houten", "Huizen", "Hulst",
        "IJsselstein", "Kaag en Braassem", "Kampen", "Kapelle", "Katwijk", "Kerkrade",
        "Koggenland", "Krimpen aan den IJssel", "Krimpenerwaard", "Laarbeek", "Land van Cuijk", "Landgraaf",
        "Landsmeer", "Lansingerland", "Laren", "Leeuwarden", "Leiden", "Leiderdorp",
        "Leidschendam-Voorburg", "Lelystad", "Leudal", "Leusden", "Lingewaard", "Lisse",
        "Lochem", "Loon op Zand", "Lopik", "Losser", "Maasdriel", "Maasgouw",
        "Maashorst", "Maassluis", "Maastricht", "Medemblik", "Meerssen", "Meierijstad",
        "Meppel", "Middelburg", "Midden-Delfland", "Midden-Drenthe", "Midden-Groningen", "Moerdijk",
        "Molenlanden", "Montferland", "Montfoort", "Mook en Middelaar", "Neder-Betuwe", "Nederweert",
        "Nieuwegein", "Nieuwkoop", "Nijkerk", "Nijmegen", "Nissewaard", "Noardeast-Fryslân",
        "Noord-Beveland", "Noordenveld", "Noordoostpolder", "Noordwijk", "Nuenen c.a.", "Nunspeet",
        "Oegstgeest", "Oirschot", "Oisterwijk", "Oldambt", "Oldebroek", "Oldenzaal",
        "Olst-Wijhe", "Ommen", "Oost Gelre", "Oosterhout", "Ooststellingwerf", "Oostzaan",
        "Opmeer", "Opsterland", "Oss", "Oude IJsselstreek", "Ouder-Amstel", "Oudewater",
        "Overbetuwe", "Papendrecht", "Peel en Maas", "Pekela", "Pijnacker-Nootdorp", "Purmerend",
        "Putten", "Raalte", "Reimerswaal", "Renkum", "Renswoude", "Reusel-De Mierden",
        "Rheden", "Rhenen", "Ridderkerk", "Rijssen-Holten", "Rijswijk", "Roerdalen",
        "Roermond", "Roosendaal", "Rotterdam", "Rozendaal", "Rucphen", "Schagen",
        "Scherpenzeel", "Schiedam", "Schiermonnikoog", "Schouwen-Duiveland", "Simpelveld", "Sint-Michielsgestel",
        "Sittard-Geleen", "Sliedrecht", "Sluis", "Smallingerland", "Soest", "Someren",
        "Son en Breugel", "Stadskanaal", "Staphorst", "Stede Broec", "Steenbergen", "Steenwijkerland",
        "Stein", "Stichtse Vecht", "Súdwest-Fryslân", "Terneuzen", "Terschelling", "Texel",
        "Teylingen", "Tholen", "Tiel", "Tietjerksteradeel", "Tilburg", "Tubbergen",
        "Twenterand", "Tynaarlo", "Uitgeest", "Uithoorn", "Urk", "Utrecht",
        "Utrechtse Heuvelrug", "Vaals", "Valkenburg aan de Geul", "Valkenswaard", "Veendam", "Veenendaal",
        "Veere", "Veldhoven", "Velsen", "Venlo", "Venray", "Vijfheerenlanden",
        "Vlaardingen", "Vlieland", "Vlissingen", "Voerendaal", "Voorne aan Zee", "Voorschoten",
        "Voorst", "Vught", "Waadhoeke", "Waalre", "Waalwijk", "Waddinxveen",
        "Wageningen", "Wassenaar", "Waterland", "Weert", "West Betuwe", "West Maas en Waal",
        "Westerkwartier", "Westerveld", "Westervoort", "Westerwolde", "Westland", "Weststellingwerf",
        "Wierden", "Wijchen", "Wijdemeren", "Wijk bij Duurstede", "Winterswijk", "Woensdrecht",
        "Woerden", "Wormerland", "Woudenberg", "Zaanstad", "Zaltbommel", "Zandvoort",
        "Zeewolde", "Zeist", "Zevenaar", "Zoetermeer", "Zoeterwoude", "Zuidplas",
        "Zundert", "Zutphen", "Zwartewaterland", "Zwijndrecht", "Zwolle",
    ];

    // --- Identity / fiscal ---

    /// <summary>
    /// A valid Burgerservicenummer (BSN): 9 digits that satisfy the official "elfproef" (11-test).
    /// Fictitious - only for use as test data.
    /// </summary>
    /// <returns>A 9-digit BSN string that passes the 11-test.</returns>
    public string Bsn() => BsnLikeNumber();

    /// <summary>
    /// A valid RSIN (Rechtspersonen en Samenwerkingsverbanden Informatienummer): 9 digits that satisfy
    /// the same "elfproef" as the BSN.
    /// </summary>
    /// <returns>A 9-digit RSIN string that passes the 11-test.</returns>
    public string Rsin() => BsnLikeNumber();

    /// <summary>
    /// A BTW-nummer (VAT number) in the legal-entity format: <c>NL</c> + a 9-digit number that passes
    /// the 11-test (as an RSIN does) + <c>B</c> + a 2-digit sub-number, e.g. <c>NL123456782B01</c>.
    /// (Sole-proprietor BTW-identificatienummers use a different, randomized scheme; this generator
    /// produces the classic rechtspersoon form.)
    /// </summary>
    /// <returns>A BTW-nummer string.</returns>
    public string BtwNummer() => $"NL{BsnLikeNumber()}B{random.Next(1, 100):00}";

    /// <summary>A KvK-nummer (Chamber of Commerce number): 8 digits. There is no public checksum.</summary>
    /// <returns>An 8-digit KvK-nummer string.</returns>
    public string KvkNummer() => random.Next(1, 10) + random.Digits(7);

    /// <summary>A vestigingsnummer (KvK establishment number): 12 digits. There is no public checksum.</summary>
    /// <returns>A 12-digit vestigingsnummer string.</returns>
    public string Vestigingsnummer() => random.Next(1, 10) + random.Digits(11);

    /// <summary>An AGB-code (healthcare provider code): 8 digits. There is no public checksum.</summary>
    /// <returns>An 8-digit AGB-code string.</returns>
    public string AgbCode() => random.Digits(8);

    /// <summary>
    /// A BIG-nummer (healthcare professionals register): 11 digits. There is no public checksum, so
    /// only the shape is realistic.
    /// </summary>
    /// <returns>An 11-digit BIG-nummer string.</returns>
    public string BigNummer() => random.Next(1, 10) + random.Digits(10);

    /// <summary>A UZOVI-code (health insurer identifier): 4 digits. There is no public checksum.</summary>
    /// <returns>A 4-digit UZOVI-code string.</returns>
    public string UzoviCode() => random.Digits(4);

    // --- Bank ---

    /// <summary>
    /// A valid Dutch IBAN: <c>NL</c> + 2 mod-97 check digits (ISO&#160;13616) + a 4-letter bank code +
    /// a 10-digit account number, e.g. <c>NL91ABNA0417164300</c>. The check digits are computed so the
    /// IBAN passes mod-97 validation.
    /// </summary>
    /// <returns>An 18-character Dutch IBAN string.</returns>
    public string Iban()
    {
        var bank = random.PickFrom(BankCodes);
        var account = random.Digits(10);
        var check = 98 - Checksums.Mod97($"{bank}{account}NL00");
        return $"NL{check:00}{bank}{account}";
    }

    /// <summary>A BIC/SWIFT code of a Dutch bank, e.g. <c>INGBNL2A</c>.</summary>
    /// <returns>An 8-character Dutch BIC string.</returns>
    public string Bic() => random.PickFrom(Bics);

    /// <summary>
    /// A classic (pre-IBAN) Dutch bank account number: 9 digits that satisfy the bank "elfproef"
    /// (the weighted 9..1 sum is divisible by 11).
    /// </summary>
    /// <returns>A 9-digit bank account number string that passes the bank 11-test.</returns>
    public string Bankrekeningnummer() => BankAccountNumber();

    /// <summary>
    /// A valid EAN-13 / GTIN-13 barcode: 12 random digits plus the GS1 mod-10 check digit,
    /// e.g. <c>8712345678906</c>.
    /// </summary>
    /// <returns>A 13-digit barcode string with a valid GS1 check digit.</returns>
    public string EanCode()
    {
        var body = random.Digits(12);
        return body + Checksums.Gs1CheckDigit(body);
    }

    // --- Address / contact ---

    /// <summary>
    /// A Dutch postcode in the format <c>1234 AB</c>: four digits (1000-9999) and two uppercase
    /// letters, avoiding the letter pairs PostNL does not issue (SS, SD, SA).
    /// </summary>
    /// <returns>A postcode string such as <c>"1234 AB"</c>.</returns>
    public string Postcode()
    {
        var digits = random.Next(1000, 10000);
        string letters;
        do
        {
            letters = $"{(char)('A' + random.Next(26))}{(char)('A' + random.Next(26))}";
        }
        while (letters is "SS" or "SD" or "SA");

        return $"{digits} {letters}";
    }

    /// <summary>
    /// A Dutch number plate ("kenteken") in one of the common modern sidecodes (e.g. <c>XX-999-X</c>),
    /// using only the letters that are actually issued (no vowels, no C/Q/W/Y).
    /// </summary>
    /// <returns>A kenteken string such as <c>"GK-123-D"</c>.</returns>
    public string Kenteken() => random.FromPattern(random.PickFrom(KentekenPatterns), PlateLetters);

    /// <summary>
    /// A Dutch mobile phone number in national format without separators: <c>06</c> followed by 8
    /// digits, e.g. <c>0612345678</c>.
    /// </summary>
    /// <returns>A 10-digit mobile number string starting with <c>06</c>.</returns>
    public string Mobiel() => $"06{random.Digits(8)}";

    /// <summary>
    /// A Dutch landline number in national format without separators: a real area code followed by a
    /// subscriber number, always 10 digits in total, e.g. <c>0201234567</c>.
    /// </summary>
    /// <returns>A 10-digit landline number string.</returns>
    public string VastTelefoonnummer()
    {
        var (area, subscriberLength) = random.PickFrom(AreaCodes);
        return $"{area}{random.Digits(subscriberLength)}";
    }

    /// <summary>
    /// A plausible Dutch passport/ID document number: 2 uppercase letters followed by 7 digits, e.g.
    /// <c>NX1234567</c>. There is no public checksum, so only the shape is realistic.
    /// </summary>
    /// <returns>A 9-character document number string.</returns>
    public string Paspoortnummer() => random.FromPattern("??#######");

    /// <summary>A Dutch driving-licence number: 10 digits. There is no public checksum.</summary>
    /// <returns>A 10-digit driving-licence number string.</returns>
    public string Rijbewijsnummer() => random.Digits(10);

    /// <summary>The name of one of the twelve Dutch provinces.</summary>
    /// <returns>A province name such as <c>"Utrecht"</c>.</returns>
    public string Provincie() => random.PickFrom(Provincies);

    /// <summary>The name of a Dutch municipality (gemeente).</summary>
    /// <returns>A municipality name such as <c>"Amsterdam"</c>.</returns>
    public string Gemeente() => random.PickFrom(Gemeenten);

    // --- Helpers (thin wrappers over the shared Checksums / RandomExtensions in XModelBuilder) ---

    // 9 digits passing the BSN/RSIN "elfproef" (the 9th digit has weight -1, so it equals the weighted
    // sum of the first 8 modulo 11). Retries on the rare 10, which has no single-digit representation.
    private string BsnLikeNumber()
    {
        while (true)
        {
            var body = random.Next(1, 10) + random.Digits(7); // 8 digits, no leading zero
            var check = Checksums.Mod11WeightedSum(body, ElfproefBodyWeights);
            if (check != 10)
            {
                return body + check;
            }
        }
    }

    // 9 digits passing the bank "elfproef" (the 9th digit has weight +1, so the whole weighted 9..1 sum
    // is divisible by 11).
    private string BankAccountNumber()
    {
        while (true)
        {
            var body = random.Next(1, 10) + random.Digits(7);
            var check = (11 - Checksums.Mod11WeightedSum(body, ElfproefBodyWeights)) % 11;
            if (check != 10)
            {
                return body + check;
            }
        }
    }
}
