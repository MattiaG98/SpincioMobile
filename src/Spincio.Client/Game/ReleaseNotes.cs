using System.Globalization;
using Microsoft.JSInterop;

namespace Spincio.Client.Game;

/// <summary>What changed in one version, told to the players (the "Novità" panel). CHANGELOG.md has the details.</summary>
public sealed record ReleaseNote(string Version, DateOnly Date, string Title, IReadOnlyList<string> Changes)
{
    private static readonly string[] Months =
        ["gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre"];

    /// <summary>"12 novembre 2026": written out here, since the browser app may lack Italian culture data.</summary>
    public string DateText => string.Create(CultureInfo.InvariantCulture, $"{Date.Day} {Months[Date.Month - 1]} {Date.Year}");
}

/// <summary>
/// The release notes, newest first. Every version of the app adds its note here, in plain words for the players, together
/// with its CHANGELOG.md entry: a test fails if the current version has none.
/// </summary>
public static class ReleaseNotes
{
    public static IReadOnlyList<ReleaseNote> All { get; } =
    [
        new("1.14.0", new(2026, 11, 13), "Segnala le mosse di Titti",
        [
            "Titti ha giocato una carta che non ti convince? Dal menu di pausa, o alla fine di una smazzata, tocca "
                + "«Segnala una mossa di Titti»: scegli la mossa, tocca cosa non va, la carta che avrebbe dovuto giocare e "
                + "spiega in due parole come avrebbe dovuto ragionare.",
            "La segnalazione arriva con la partita esatta: la rigiochiamo con le stesse carte e, se la CPU sbagliava "
                + "davvero, la correggiamo nelle prossime versioni.",
            "Funziona anche offline: la segnalazione parte da sola appena c'è connessione. Non inviamo il tuo nome.",
        ]),
        new("1.13.0", new(2026, 11, 13), "Le novità, a portata di mano",
        [
            "Nuova sezione «Novità»: dal menu iniziale vedi che cosa cambia a ogni versione di Spincio.",
            "Quando arriva una versione nuova, il pulsante «Novità» si illumina finché non l'hai letta.",
        ]),
        new("1.12.0", new(2026, 11, 12), "CPU più intelligenti, compagno compreso",
        [
            "Il compagno e gli avversari ragionano sulle carte non ancora uscite: per ogni carta che possono giocare "
                + "immaginano le risposte degli altri fino a fine giro e scelgono quella che conviene.",
            "Valutano le carte prese per i punti che portano davvero a fine smazzata: carte, denari, sette, settebello, "
                + "rebello e scala di denari.",
            "Sono molto più forti: allo stesso livello vincono circa 6 partite su 10 contro le CPU di prima.",
            "A volte il compagno tiene l'asso invece di fare subito uno spazzino con una carta sola: lo usa dopo, per "
                + "prendere una tavola più ricca.",
            "Anche le CPU delle partite online sono le nuove.",
        ]),
        new("1.11.0", new(2026, 10, 22), "Asso all'ultima giocata e Spincione a 101",
        [
            "L'asso fa spazzino anche all'ultima giocata della smazzata, sia nello Spincio sia nello Spincione. Con le "
                + "altre carte, all'ultima giocata lo spazzino continua a non valere.",
            "Lo Spincione si gioca a 101 punti; in caso di pareggio esatto lo spareggio si gioca a 31.",
            "Le partite salvate con le regole di prima non si possono riprendere.",
        ]),
        new("1.10.0", new(2026, 10, 17), "La gemella",
        [
            "Nello Spincione, se prendi la stessa carta che giochi (la sua gemella dell'altro mazzo) fai 2 punti; se "
                + "svuoti anche la tavola sono 3: «Gemella con cappella».",
            "Il riepilogo di fine smazzata mostra i punti delle gemelle.",
        ]),
        new("1.9.0", new(2026, 10, 17), "Arriva lo Spincione",
        [
            "Nuova modalità Spincione, da scegliere nel menu iniziale o nella pagina Online: due mazzi, prima mano da 4 "
                + "carte, accusi della prima mano doppi e i nuovi accusi colore e scala.",
            "Nello Spincione una presa tutta dello stesso seme vale un punto in più; settebello e rebello valgono un "
                + "punto per copia.",
            "La pagina Regole ha una sezione dedicata allo Spincione.",
        ]),
        new("1.8.1", new(2026, 10, 15), "Online senza link",
        [
            "Per giocare online basta aprire «Gioca online con gli amici» e scrivere il tuo nome: chi ha l'app aperta "
                + "compare nella lista e puoi invitarlo.",
            "Se nessuno giocava da un po', il primo collegamento può richiedere fino a un minuto.",
        ]),
        new("1.8.0", new(2026, 10, 15), "Partite online più robuste",
        [
            "Se il server si riavvia durante una partita online, la partita riparte da dove era, con le stesse carte e lo "
                + "stesso punteggio.",
            "Se la connessione salta, l'app riprova da sola per 3 minuti.",
        ]),
        new("1.7.2", new(2026, 10, 13), "L'asso non si butta",
        [
            "Le CPU non calano più l'asso a tavola vuota: lo tengono per prendere tutta la tavola più avanti.",
        ]),
        new("1.7.1", new(2026, 10, 13), "Server che si sveglia",
        [
            "Mentre l'app si collega, la pagina Online avvisa che un server spento può metterci fino a un minuto a "
                + "svegliarsi.",
        ]),
        new("1.7.0", new(2026, 10, 13), "Giocatori online e inviti",
        [
            "Nella pagina Online vedi chi ha l'app aperta e lo inviti con un tocco.",
            "Chi riceve un invito lo vede in alto, in qualunque schermata, e sceglie se giocare con te o contro di te.",
        ]),
        new("1.6.0", new(2026, 10, 13), "Gioca online con gli amici",
        [
            "«Gioca online con gli amici» è sempre nel menu; nella stanza, «Invita gli amici» manda il link su WhatsApp, "
                + "Messaggi e simili.",
            "La scritta dei punti è più piccola e più rapida.",
        ]),
        new("1.5.4", new(2026, 10, 11), "Punti insieme alla mossa",
        [
            "La scritta dei punti di uno spazzino parte insieme alla carta, dal giocatore che lo fa.",
        ]),
        new("1.5.3", new(2026, 10, 11), "Accuso più pulito",
        [
            "Il pulsante «Accusa» è più compatto e, dopo l'accuso, non resta più un'etichetta sullo schermo.",
        ]),
        new("1.5.2", new(2026, 10, 11), "Ritratti rinnovati",
        [
            "Titti, Tito e Vava hanno i capelli rossi ricci; Tito è di colore.",
        ]),
        new("1.5.1", new(2026, 10, 9), "Capelli rossi",
        [
            "I ritratti delle CPU hanno i capelli rossi ricci.",
        ]),
        new("1.5.0", new(2026, 10, 9), "I volti delle CPU",
        [
            "Ogni CPU ha il suo ritratto accanto al nome: Titti è il tuo compagno, Tito e Vava gli avversari.",
        ]),
        new("1.4.0", new(2026, 10, 9), "Contributi",
        [
            "Nuovo pannello «Contributi» nel menu, con chi ha aiutato a creare Spincio.",
        ]),
        new("1.3.0", new(2026, 10, 9), "Mariana",
        [
            "Quando un fante prende un 5 e un 3 compare la scritta MARIANA.",
        ]),
        new("1.2.1", new(2026, 10, 9), "Aggiornamenti con un tocco",
        [
            "Quando è pronta una versione nuova compare «Aggiorna»: un tocco e l'app si aggiorna. La partita in corso "
                + "resta salvata.",
        ]),
        new("1.2.0", new(2026, 10, 8), "Statistiche",
        [
            "Nuove statistiche dal menu: partite vinte, serie di vittorie, spazzini, accusi e altro.",
            "CPU più forti a entrambi i livelli.",
        ]),
        new("1.1.0", new(2026, 10, 8), "Punti in volo",
        [
            "Quando una squadra fa punti, «+N» vola sul punteggio.",
        ]),
        new("1.0.0", new(2026, 10, 7), "La prima versione",
        [
            "Spincio 2 contro 2 contro la CPU, con partite salvate, regole, opzioni e animazioni.",
        ]),
    ];

    /// <summary>The note of the newest version.</summary>
    public static ReleaseNote Latest => All[0];
}

/// <summary>Remembers on the device the newest release note the player has opened, to light up "Novità" when there is a newer one.</summary>
public sealed class ReleaseNotesService(IJSRuntime js)
{
    private const string Key = "spincio.notesSeen";

    /// <summary>True if the newest note has not been opened on this device (also on a first visit).</summary>
    public async Task<bool> HasUnreadAsync()
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", Key) != ReleaseNotes.Latest.Version;
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
            return false;
        }
    }

    public async Task MarkReadAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, ReleaseNotes.Latest.Version);
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }
    }
}
