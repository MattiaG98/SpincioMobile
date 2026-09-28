# Crediti e licenze

Spincio è un progetto hobby. Il regolamento (`docs/SPEC.md`) è scritto da zero; nessun testo è copiato dal web.

## Grafica
| Elemento | Origine | Licenza |
|---|---|---|
| Carte (semi, pip, figure) | Disegni SVG originali in questo repository (`CardFace.razor`, `SuitGlyph.razor`, ADR 0009) | Stessa licenza del progetto |
| Icone dell'app (`favicon.png`, `icon-192.png`, `icon-512.png`) | Template Blazor WebAssembly di .NET | MIT (© .NET Foundation) — da sostituire con icone proprie |

Non vengono usate grafiche né marchi di editori di carte da gioco (es. Dal Negro, Modiano).

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
