<a name="start-building"></a>

<p align="center">
<img src="img/banner-ignite-26.png" alt="Microsoft Ignite 2026" width="100%"/>
</p>

# [Microsoft Ignite 2026](https://ignite.microsoft.com)

## 🔥 THR979: Follow the Tokens: Finding the Real Cost of Your Foundry AI Models

### Session description

This project shows how to retrieve and calculate Microsoft Foundry model deployment costs programmatically. Using PowerShell, .NET, application authentication, Azure Monitor metrics, and Azure pricing APIs, it collects model usage and pricing data to calculate costs per request and deployment. The implementation is designed for unattended execution and can be integrated into automated FinOps, reporting, budgeting, and governance workflows.

### 🚀 Getting started

This theater session is self-paced for attendees. Use the steps below to run the content on your own.

1. Clone this repository.
2. Install prerequisites:
   - [.NET 10 SDK](https://dotnet.microsoft.com/)
   - [PowerShell 7+](https://learn.microsoft.com/powershell/)
   - [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (needed for `run-agent.ps1`)
3. Pick a path:
   - **.NET path (recommended for unattended reporting)**  
     Follow [`src/dotnet/README.md`](src/dotnet/README.md):
     1. Create an Entra app registration and client secret
     2. Assign **Monitoring Reader** to the Foundry/Cognitive Services resource
     3. Copy `src/dotnet/.env.example` to `.env` and fill in values
     4. Run `dotnet run` from `src/dotnet`
   - **PowerShell path (price lookup and agent call demos)**  
     Follow [`src/powershell/ReadMe.md`](src/powershell/ReadMe.md):
     1. Run `get-prices.ps1` to export Foundry model pricing data
     2. For `run-agent.ps1`, sign in with `az login` and set the endpoint/agent parameters
4. Use output from both paths to compare token usage and estimated cost per deployment.

### 🎯 Learning outcomes

By the end of this session, you will be able to:

- Retrieve Microsoft Foundry model usage and pricing data programmatically.
- Calculate costs per request and deployment by combining Azure Monitor usage metrics with Azure pricing data.
- Integrate unattended cost calculations into automated FinOps, reporting, budgeting, and governance workflows.

### 💻 Technologies used

- Microsoft Foundry
- PowerShell
- .NET
- Application authentication
- Azure Monitor metrics
- Azure pricing APIs

### 📚 Continue your learning

Pick your next step based on your learning style:

| Resource | What you'll get |
|----------|-----------------|
| **[Ignite 2026 Next Steps](https://aka.ms/ignite26-next-steps)** | Additional session repos and materials from Microsoft Ignite 2026 |
| **[Microsoft AI Skills Navigator](https://aka.ms/ignite26-asn)** | Discover AI skills, learning paths, and resources for your next step |
| **[Microsoft Learn](https://aka.ms/ignite26-learn)** | Official documentation and guided learning paths on these topics |
| **[Microsoft Foundry Community](https://aka.ms/MicrosoftFoundryDiscord-Ignite26)** | Connect with other learners and experts in our Discord community |

### 👥 Content owner

[Toni Pohl](https://github.com/tonipohl)

### ⚖️ Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.

Any use of third-party trademarks or logos are subject to those third-party's policies.
