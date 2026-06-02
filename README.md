# AIPaste 🚀

A Windows system-tray companion that **transforms your clipboard with AI**. Rewrite, translate, or run your own custom prompts on any copied text — all from a polished, keyboard-friendly popup.

![.NET](https://img.shields.io/badge/.NET-9.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)
![License](https://img.shields.io/badge/License-MIT-green)
![Version](https://img.shields.io/badge/Version-2.1.0-purple)

---

## 🆕 What's new in **v2.1.0**

Copilot integration rebuilt around the direct HTTP API — no CLI or SDK at runtime.

- **In-app sign-in** – GitHub OAuth browser device flow; the token is stored DPAPI-encrypted and reused across restarts. No `copilot` CLI install or login.
- **No CLI/SDK dependency** – dropped the bundled Copilot CLI and SDK; the app talks straight to the Copilot HTTP API, so it never needs rebuilding to track CLI updates.
- **All models supported** – per-request routing between the `/chat/completions` and `/responses` endpoints, so newer models (codex, gpt-5.x, mai-code) work alongside Claude and Gemini.
- **Request timing** – status bar now shows how long each request took.

---

## 🆕 What's new in **v2.0.0**

A complete UI redesign and rearchitecture. AIPaste went from a stack of basic WinForms dialogs to a modern app shell with a unified workflow.

| | v1.x | v2.0 |
|---|---|---|
| **Tray menu** | 4 items (Open / Configure / Custom Actions / Exit) | 2 items (Open / Exit) — everything else lives inside the popup |
| **UI style** | Default WinForms gray | Dark theme · purple accent · rounded surfaces · custom-painted controls |
| **Mode picker** | Dropdown | Pill-shaped chip toolbar (Rewrite / Translate / Custom) |
| **Tone & Language** | Dropdowns | Chip rows |
| **Model + Process** | Two separate controls | Single **split-button pill** (model picker + ✨ Process) |
| **Configuration** | Modal dialog window | In-app **Settings** pane (no window switching) |
| **Custom Actions** | Separate window | In-app **Custom Actions** pane with sidebar list + detail editor |
| **Navigation** | Tray right-click | Left **activity rail** (VS Code style) — Process / Custom Actions / Settings |
| **Status** | Hidden | Bottom **status bar** with auth dot, current model, action count, contextual hint |
| **Window** | Standard title bar | **Borderless** with custom dark bar; drag-to-move from top |
| **Keyboard** | Limited | `Esc` to close · `Enter` to process · `Ctrl+1/2/,` to switch panes |

---

## 📸 Screenshots

### Main Popup — Process Pane
![Main Popup](docs/images/main-popup.png)

### Settings — GitHub Copilot
![GitHub Copilot Settings](docs/images/config-copilot.png)

### Settings — Azure OpenAI
![Azure OpenAI Settings](docs/images/config-azure.png)

### Custom Actions
![Custom Actions](docs/images/custom-actions.png)

---

## ✨ Features

- **AI-powered text transforms** – Rewrite, translate, or run custom prompts on any clipboard text
- **Two providers** – GitHub Copilot (built-in browser sign-in, no CLI/SDK) or Azure OpenAI (bring your own endpoint + key)
- **Per-request model picker** – Pick any available model on the fly via the model pill (routes automatically to the right Copilot API)
- **Tone control** – Professional · Casual · Informative · Enthusiastic
- **Translation** – Hindi & Gujarati out of the box (more easy to add)
- **Custom Actions** – Reusable AI prompts with `{text}` placeholder; manage from the in-app pane
- **Streaming output** – AI result appears live, character by character
- **Status bar** – Always-on auth indicator + current model
- **Single-instance** – Activate the running tray app instead of launching duplicates
- **System tray** – Quietly minimised; one click to open, one click to quit

---

## 🔧 Prerequisites

- Windows 10 / 11
- .NET 9.0 Desktop Runtime
- One of the following:
  - **GitHub Copilot** – Active GitHub Copilot subscription (sign in from inside the app via GitHub's browser device flow — no CLI or SDK required)
  - **Azure OpenAI** – Azure subscription with a deployed OpenAI resource

---

## 📥 Installation

### Option 1 — Download Release (recommended)

1. Grab the latest from [Releases](../../releases)
2. Extract the archive somewhere persistent (e.g. `C:\Program Files\AIPaste\`)
3. Run `AIPaste.exe`
4. *(Optional)* Right-click → **Pin to taskbar** for one-click access

### Option 2 — Build from Source

```bash
git clone https://github.com/tannadhruv92/AIPaste.git
cd AIPaste
dotnet build -c Release
```

---

## ⚙️ Configuration

When you first run AIPaste, it'll prompt you to configure a provider. Inside the popup, click the **⚙ Settings** icon at the bottom of the rail.

### GitHub Copilot

1. In **Settings**, select the **⚡ GitHub Copilot** card
2. Click **Sign in** — AIPaste copies the device code and opens GitHub in your browser
3. Paste the code, approve access, and the status pill turns green ✓ Authenticated
4. Choose your **Default Model** and click **Save**

Sign-in uses GitHub's OAuth device flow; the resulting token is stored DPAPI-encrypted and reused across restarts. The app talks directly to the Copilot HTTP API — no CLI, SDK, or background process.

### Azure OpenAI

1. Create an Azure OpenAI resource in the [Azure Portal](https://portal.azure.com)
2. Deploy a model (e.g. `gpt-4o`, `gpt-4`)
3. Note your:
   - **Endpoint** – `https://your-resource.openai.azure.com`
   - **API Key** – Azure Portal → *Keys and Endpoint*
   - **Deployment ID** – Name of your deployed model
4. In AIPaste **Settings**, select the **☁ Azure OpenAI** card
5. Fill in API key, endpoint, and deployment ID, then **Save**

API keys are encrypted with Windows DPAPI before being stored in `config.json`.

---

## 🚀 Usage

1. **Run** `AIPaste.exe` — it minimises to the system tray
2. **Copy** any text (`Ctrl+C`)
3. **Click** the AIPaste tray icon (or double-click) to open the popup
4. **Pick a mode** — Rewrite, Translate, or Custom
5. **Tweak chips** — Tone, Language, or Action depending on mode
6. *(Optional)* Click the model name in the split button to switch models for this request only
7. **Press Enter** (or click ✨ **Process**) — result streams into the AI Result card
8. **✓ Accept & Copy** — copies the result to your clipboard and closes the popup

### Keyboard shortcuts

| Key | Action |
|---|---|
| `Enter` | Run Process (the split button has default focus) |
| `Esc` | Close the popup |
| `Ctrl+1` | Process pane |
| `Ctrl+2` | Custom Actions pane |
| `Ctrl+,` | Settings pane |

---

## 🎯 Custom Actions

Reusable AI prompts for repetitive tasks. Switch to the **📋 Custom Actions** pane from the rail (or `Ctrl+2`).

1. Click **＋ New Action**
2. Enter a **Name** and a **Prompt Template** — use `{text}` as the placeholder for clipboard content
3. Click **💾 Save**
4. Your action now appears as a chip in **Custom** mode on the Process pane

### Example actions

| Name | Prompt template |
|------|-----------------|
| Fix Grammar | `Fix any grammar and spelling errors in the following text: {text}` |
| Summarize | `Summarize the following text in 2–3 sentences: {text}` |
| Make Bullet Points | `Convert the following text into bullet points: {text}` |
| Explain Simply | `Explain the following in simple terms a 10-year-old would understand: {text}` |
| Email Reply | `Write a professional email reply to: {text}` |

---

## 🏗️ Building from Source

### Requirements

- **.NET 9.0 SDK**
- **Visual Studio 2022** or **VS Code** (with C# Dev Kit)

### Steps

```bash
git clone https://github.com/tannadhruv92/AIPaste.git
cd AIPaste
dotnet restore
dotnet build -c Release
dotnet run                      # for local testing
```

### Project structure

```
AIPaste/
├── Program.cs                  # Entry point + single-instance pipe
├── MainForm.cs                 # Hidden host form + system tray
├── MainForm.Designer.cs
├── ConfigManager.cs            # Settings persistence (DPAPI-encrypted)
├── Copilot/                    # Direct GitHub Copilot HTTP integration (no CLI/SDK)
│   ├── CopilotAuth.cs          # OAuth device flow + token cache
│   ├── CopilotApiClient.cs     # Models + chat streaming; routes /chat/completions vs /responses
│   └── CopilotModel.cs         # Model DTO
├── UI/
│   ├── Theme.cs                # Colours, fonts, metrics
│   ├── GraphicsExt.cs          # Rounded-rect helpers
│   ├── AppShellForm.cs         # Main popup window (rail + content + status bar)
│   ├── Controls/
│   │   ├── ChipButton.cs       # Pill-shaped chip
│   │   ├── ChipGroup.cs        # Labelled chip row (Mode / Tone / Language / Action)
│   │   ├── SurfaceCard.cs      # Rounded surface container
│   │   ├── SplitActionButton.cs# Model + ✨ Process pill
│   │   ├── ActivityRail.cs     # Left navigation rail
│   │   └── StatusBar.cs        # Bottom ambient status bar
│   └── Panes/
│       ├── ProcessPane.cs      # Default pane — chip toolbar + clipboard + result
│       ├── SettingsPane.cs     # Provider · auth · default model
│       └── CustomActionsPane.cs# List + detail editor for saved prompts
└── config.json                 # User configuration (auto-created next to the EXE)
```

---

## 🔒 Security

- API keys are encrypted with Windows DPAPI (per-user) before persisting to `config.json`
- All data stays local — nothing is sent anywhere except your configured AI provider
- Configuration lives next to `AIPaste.exe` (portable)

---

## 🤝 Contributing

Pull requests welcome! For larger changes please open an issue first.

1. Fork the repo
2. Create a feature branch — `git checkout -b feature/AmazingFeature`
3. Commit — `git commit -m 'Add some AmazingFeature'`
4. Push — `git push origin feature/AmazingFeature`
5. Open a Pull Request

---

## 📄 License

MIT — see [LICENSE](LICENSE).

---

## 🙏 Acknowledgments

- [GitHub Copilot HTTP API](https://docs.github.com/en/copilot)
- [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI)

---

Made with ❤️ for clipboard power users.
