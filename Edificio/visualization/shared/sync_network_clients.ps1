$ErrorActionPreference='Stop'
$campusUnity=Join-Path $PSScriptRoot '../unity'
foreach($campusName in @('UnityVisualization','CampusCardboard')) {
    foreach($campusSource in @('AnalysisNetworkClient.cs','ReinforcementPanel.cs')) {
        $campusTarget=Join-Path $campusUnity "$campusName/Assets/Scripts/$campusSource"
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $campusSource) -Destination $campusTarget -Force
    }
}
