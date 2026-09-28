# M8 — App nativa: opzioni (non implementata)

> **Stato:** saltata su indicazione del proprietario ("evita fasi che richiedono pagamenti").
> Nel frattempo la PWA si **installa già** dal browser: Chrome/Android "Aggiungi a schermata Home", Safari/iOS "Condividi → Aggiungi a Home".

## Perché costa
| Store | Costo (indicativo, da verificare al momento) |
|---|---|
| Google Play | Registrazione sviluppatore una tantum (~25 USD) |
| Apple App Store | Apple Developer Program annuale (~99 USD/anno) + un Mac per firmare e pubblicare |

## Opzioni tecniche

| Opzione | Cosa fa | Pro | Contro |
|---|---|---|---|
| **TWA** (Trusted Web Activity, es. Bubblewrap/PWABuilder) | Incapsula la PWA pubblicata in un'app Android | Nessun codice nuovo; aggiornamenti = redeploy del sito | Solo Android; richiede Digital Asset Links sul dominio |
| **PWABuilder → pacchetto iOS** | WKWebView con la PWA | Riuso totale | Review Apple severa sulle app "solo web" |
| **Capacitor** | Contenitore nativo con i file statici della PWA | Android + iOS, plugin nativi | Toolchain Node + Android Studio/Xcode |
| **.NET MAUI Blazor Hybrid** (ADR 0001) | App nativa che ospita i componenti Razor | Stesso C#, nessun WebAssembly (più veloce: L2 con più mondi) | Da rifattorizzare il client in una Razor Class Library condivisa |

## Raccomandazione (quando si vorrà fare)
1. **Android per primo con TWA**: costo minimo, zero codice, usa il deploy di GitHub Pages.
2. iOS solo se c'è domanda reale, preferibilmente con **MAUI Blazor Hybrid**, che rende anche L2 più forte grazie al codice nativo.
3. Prima di pubblicare: verificare il marchio "Spincio" (EUIPO, UIBM), sostituire le icone del template .NET e preparare privacy policy e schede dello store.

## Lavoro preparatorio già fatto
- Manifest PWA con nome, colori, icone 192/512 e `display: standalone`.
- Service worker per l'uso offline; `<base href>` indipendente dal percorso di hosting.
- Nessun dato personale raccolto; online solo nome scelto e token di posto casuale.
