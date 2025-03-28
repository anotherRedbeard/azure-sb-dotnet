if [ -z "$1" ]; then
    echo "Usage: deploy.sh all|infra|function"
    #exit 1
fi

if [ "$1" == "all" ]; then
    echo "Deploying all"
    echo "Deploying infra"
    az deployment sub create --subscription <subscription-Id> --location <location> --name dotnetcore-azfunction-deploy --parameters ./iac/bicep/main.bicepparam
    echo "Deploying function"
    cd ./src
    func azure functionapp publish <function-name> --dotnet-version 8.0
    cd ../
elif [ "$1" == "infra" ]; then
    echo "Deploying infra"
    az deployment sub create --subscription <subscription-Id> --location <location> --name dotnetcore-azfunction-deploy --parameters ./iac/bicep/main.bicepparam
elif [ "$1" == "function" ]; then
    echo "Deploying function"
    cd ./src
    func azure functionapp publish <function-name> --dotnet-version 8.0
    cd ../
else
    echo "Invalid option"
    #exit 1
fi