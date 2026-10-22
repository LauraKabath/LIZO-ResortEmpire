# GitFlow – LIZO Project / Resort Empire

Tento dokument stanovuje spoločný postup práce s GitHubom pre všetkých členov tímu **LIZO Project**. **Odteraz používame klasický GitFlow.** Bežná vývojová práca sa integruje do vetvy `develop`, **nie priamo do `main`**.

## 1. Na čo slúžia vetvy

| Vetva | Účel | Vytvára sa z | Zlučuje sa do |
| --- | --- | --- | --- |
| `main` | Stabilné vydané verzie hry | — | — |
| `develop` | Spoločná priebežná vývojová verzia | Inicializuje sa z aktuálneho `main` | — |
| `feature/SCRUM-XXX-short-name` | Práca na jednej úlohe alebo súvisiacej funkcionalite | `develop` | `develop` |
| `release/X.Y.Z` | Finalizácia, testovanie a opravy pripravovaného vydania | `develop` | `main` **aj** `develop` |
| `hotfix/X.Y.Z` | Naliehavá oprava už vydanej verzie | `main` | `main` **aj** `develop` |

Vetvy `release/*` a `hotfix/*` nevytvárame vopred. Použijeme ich až pri príprave vydania alebo pri oprave vydanej verzie. Vydania označujeme tagom, napríklad `v1.0.0`, na príslušnom commite vo vetve `main`.

## 2. Prechod na GitFlow

Doterajšie commity v `main` zostávajú zachované. Už zlúčené Pull Requesty **nevraciame ani neprerábame**.

## 3. Postup každého člena pri novej úlohe (GitHub Desktop)

1. Otvorte **GitHub Desktop** a vyberte náš repozitár.
2. Kliknite na **Fetch origin**.
3. Prepnite sa na vetvu `develop`. Ak ju ešte nemáte lokálne, vyberte ju zo vzdialených vetiev; GitHub Desktop ju sprístupní lokálne.
4. Kliknite na **Pull origin**, aby ste mali aktuálny `develop`.
5. Vyberte **Current branch → New branch** a založte vetvu **z `develop`**, napríklad `feature/SCRUM-157-buildings`.
6. Pracujte vo svojej vetve, zmeny priebežne **Commit** a **Push origin / Publish branch**.
7. Po dokončení vytvorte Pull Request na GitHube. **Skontrolujte smer PR:**

   `feature/SCRUM-157-buildings` → `develop`

8. Do PR stručne uveďte, čo ste spravili, ako ste to testovali a číslo úlohy v Jire.
9. Požiadajte iného člena o review. Po úspešnej kontrole a vyriešení konfliktov poverený člen vykoná merge.
10. Po zlúčení označte úlohu v Jire ako `Done` podľa dohody tímu. Pred ďalšou úlohou si znova aktualizujte `develop`.

**Nikdy nezačínajte novú feature vetvu zo zastaraného `main` alebo z cudzej rozpracovanej feature vetvy.**

## 4. Pravidlá Pull Requestov

- **Bežné úlohy:** `feature/*` → `develop`.
- **Príprava vydania:** vytvoriť `release/*` z `develop`; po testovaní zlúčiť do `main` **aj** späť do `develop` a vytvoriť release tag na `main`.
- **Naliehavá oprava vydanej verzie:** vytvoriť `hotfix/*` z `main`; opravu zlúčiť do `main` **aj** `develop` a označiť novým tagom. Ak je práve otvorená release vetva, opravu treba preniesť aj tam.
- Autor PR nemá sám bez kontroly rozhodovať o zlúčení. Review vykoná iný člen; integráciu koordinuje líder.
- Pred merge skontrolujte zmenené súbory, prípadné konflikty a základnú funkčnosť Unity scény.
- **Neposielajte priamo commity do `main` ani `develop`**, ak nejde o výnimočný postup vopred dohodnutý tímom.

## 5. Dôležité pri Unity scénach

Unity súbory `.unity` môžu spôsobiť konflikt aj vtedy, keď členovia upravujú rozdielne objekty tej istej scény.

- Pred úpravou spoločnej scény sa dohodnite, kto ju bude meniť a v akom poradí.
- Ak je to vhodné, pripravujte objekty ako samostatné prefaby a do spoločnej scény ich integrujte koordinovane.
- Pred začiatkom práce vždy aktualizujte `develop`.
- Pri konflikte **nevyberajte naslepo** `Accept current` alebo `Accept incoming`; mohli by ste stratiť prácu kolegu.
- Pred riešením konfliktu sa uistite, že pôvodná práca je commitnutá a pushnutá; ideálne si nechajte aj záložnú vetvu.
- Po zlúčení otvorte scénu v Unity a skontrolujte Console, kameru, UI a aktuálne objekty.

## 6. Rýchla kontrola pred vytvorením PR

- [ ] Moja vetva vznikla z aktuálneho `develop`.
- [ ] V PR je **base: `develop`**, nie `main`.
- [ ] Zmeny sú commitnuté a pushnuté.
- [ ] Unity projekt sa otvorí a testovaná funkcionalita funguje.
- [ ] Skontroloval/a som, že nemením zbytočne spoločnú scénu alebo cudzie súbory.
- [ ] PR obsahuje odkaz na Jira úlohu a stručný opis testovania.
- [ ] PR skontroluje iný člen tímu.

---

**Zapamätajte si:** `develop` je na každodenný vývoj. `main` je na vydané stabilné verzie. `feature/*` začína v `develop` a končí v `develop`.
