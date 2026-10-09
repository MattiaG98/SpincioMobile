# Crediti e licenze

Spincio è un progetto hobby. Il regolamento (`docs/SPEC.md`) è scritto da zero; nessun testo è copiato dal web.

## Contributi
Hanno contribuito a Spincio (elenco del proprietario, mostrato nell'app nel pannello "Contributi" del menu):
- Megako
- Nandone

## Grafica
| Elemento | Origine | Licenza |
|---|---|---|
| Carte (`wwwroot/cards/*.webp`) | Ritagliate da [Carte_piacentine_al_completo.jpg](https://commons.wikimedia.org/wiki/File:Carte_piacentine_al_completo.jpg), scansione di Florixc (Wikimedia Commons, 2009) — ADR 0011 | Dichiarata di pubblico dominio da chi l'ha caricata. Il disegno del mazzo può appartenere al suo editore: uso deciso dal proprietario per un progetto personale |
| Avatar delle CPU (`wwwroot/avatars/titti.svg`, `tito.svg`, `vava.svg`) | Disegni originali in SVG, fatti per Spincio | Stessa licenza del progetto |
| Icone dell'app (`favicon.png`, `icon-192.png`, `icon-512.png`) | Disegno originale (`src/Spincio.Client/Assets/icon.svg`, reso con `tools/browser-checks/render-icons.mjs`) | Stessa licenza del progetto |

Non vengono usati marchi né nomi di editori di carte da gioco. L'unica grafica di terzi sono le carte qui sopra (ADR 0011).

## Software
| Componente | Uso | Licenza |
|---|---|---|
| .NET / ASP.NET Core / Blazor WebAssembly | Runtime e framework | MIT |
| xUnit | Test | Apache-2.0 |
| Shouldly | Asserzioni nei test | BSD-3-Clause |
| FsCheck | Test property-based | BSD-3-Clause |
| bUnit | Test dei componenti Blazor | MIT |
| NetArchTest.Rules | Test di architettura | MIT |
| ASP.NET Core SignalR (server e client) | Gioco online | MIT |
| Microsoft.AspNetCore.Mvc.Testing, Microsoft.Extensions.TimeProvider.Testing | Test del server | MIT |
| Playwright (solo verifiche locali, non incluso nell'app) | Test nel browser | Apache-2.0 |
| coverlet | Copertura dei test | MIT |

## Algoritmi
- **PCG32** (M. E. O'Neill, pcg-random.org): reimplementato da zero in `src/Spincio.Engine/Pcg32.cs`; l'algoritmo di riferimento è rilasciato con licenza Apache-2.0.
