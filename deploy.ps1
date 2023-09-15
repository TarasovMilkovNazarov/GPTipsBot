# This script build image, load it on server and restart
# Prerequisites:
# 1. Create opt/docker path
# 2. Install `posh-ssh` module, which you can install by running the following command in PowerShell: Install-Module -Name posh-ssh
# 3. Install PuTTY and add it to system variables
# 4. Install OpenSSH-Win64-Portable package if not installed


# Set host address and password
$sshHost = "x.x.x.x"
$sshUser = "root"
$sshPassword = "qwerty1234"
$containerName = "suspicious_lewin"
$remotePath = "/opt/docker"
$imageFile = $containerName + ".tar"

# Create a password credential object
$securePassword = ConvertTo-SecureString $sshPassword -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential ($sshUser, $securePassword)

# Build the Docker image locally
docker build -t $containerName .
docker save -o $imageFile $containerName

# Invoke-Expression -Command "cd ""C:\Program Files\PuTTY"""
$cmd = "pscp -pw $sshPassword .\$imageFile $sshUser@${sshHost}:$remotePath"

# Run the PSCP command using Invoke-Expression
Invoke-Expression -Command $cmd

$commandLoad = "docker load -i $remotePath/$imageFile"
$commandStop = "docker stop $containerName"
$commandDelete = "docker rm $containerName"
$commandRun = "docker run --name $containerName --env-file /opt/gptips/env.txt -p 80:80/tcp --link gpt-postgres-container:postgres -d " + $containerName

# Establish SSH connection and run the commands
try {
    Write-Host "Establishing SSH connection to $sshHost..."
    $session = New-SSHSession -ComputerName $sshHost -Credential $credential
    if ($session) {
        Write-Host "SSH connection established. Running Docker commands..."
        Invoke-SSHCommand -SessionId $session.SessionId -Command $commandLoad
        Invoke-SSHCommand -SessionId $session.SessionId -Command $commandStop
        Invoke-SSHCommand -SessionId $session.SessionId -Command $commandDelete
        Invoke-SSHCommand -SessionId $session.SessionId -Command $commandRun
        Write-Host "Docker commands executed successfully."
        Remove-SSHSession -SessionId $session.SessionId
    }
    else {
        Write-Host "Failed to establish SSH connection."
    }
}
catch {
    Write-Host "Error occurred: $($_.Exception.Message)"
}