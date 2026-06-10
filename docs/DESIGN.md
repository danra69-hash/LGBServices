# Design files

## Figma Make (UI prototype)

The **LGB Services** UI was built in Figma Make and exported as a local `.make` file:

- **Local export:** [`LGB-Services.make`](./design/LGB-Services.make)
- **Generated frontend:** [`../LGBApp.Frontend/`](../LGBApp.Frontend/) (extracted from the `.make` file)

To refresh the frontend from an updated Figma export:

1. Download the latest `.make` file from Figma Make (Code → Download code, or export the `.make` file).
2. Replace `docs/design/LGB-Services.make`.
3. Re-run extraction (see `LGBApp.Frontend/Guidelines.md` or use [figma-make-extractor](https://github.com/albertsikkema/figma-make-extractor)).

## Figma (team project)

The team project (all design files for this product) is here:

**[LGB Services — Figma team project](https://www.figma.com/files/team/1635583274601385491/project/600229476?fuid=1635583272997894548)**

Open that link while logged into Figma to see every file in the project, including the LGB Services UI.

### Sharing with friends

This link opens the **project folder** inside your Figma team. To let friends collaborate on a specific file:

1. Open the project link above and click the **LGB Services** file.
2. Click **Share** (top right).
3. Invite by email, or copy a link and set permission to **can view** or **can edit**.

| Goal | Permission |
|------|------------|
| Look only | **Can view** |
| Design together | **Can edit** |
| Leave feedback | **Can view** (comments enabled) or **Can edit** |

Friends need a free [Figma account](https://www.figma.com) to edit. View-only links can work without an account depending on your share settings.

> The live Figma file is the design source of truth. The `.make` export and `LGBApp.Frontend/` code are checked into Git so the team can build and run the app without Figma open.

## Architecture diagram (local)

- [`LGB SERVICES.drawio`](./LGB%20SERVICES.drawio) — system architecture (CO SEC, admin, products, clients). Editable in [draw.io](https://app.diagrams.net/) or the Draw.io VS Code extension.
