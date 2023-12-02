echo "Logging in..."
# az login
# az account set --subscription "8c831b26-1aef-4843-9513-06420f059cd7"
az acr login --name crskragdemo

# Build and push the images
echo "Building and pushing the images..."
cd app
docker build -t crskragdemo.azurecr.io/frontend -f ./frontend/KnowledgeBase.Frontend/Dockerfile .
docker build -t crskragdemo.azurecr.io/document-processing -f ./backend/KnowledgeBase.DocumentProcessing/Dockerfile .
docker build -t crskragdemo.azurecr.io/knowledge-processing -f ./backend/KnowledgeBase.KnowledgeProcessing/Dockerfile .
docker build -t crskragdemo.azurecr.io/search-knowledge -f ./backend/KnowledgeBase.SearchKnowledge/Dockerfile .

docker push crskragdemo.azurecr.io/frontend
docker push crskragdemo.azurecr.io/document-processing
docker push crskragdemo.azurecr.io/knowledge-processing
docker push crskragdemo.azurecr.io/search-knowledge

# Deploy the images
echo "Deploying the images..."
kubectl config set-context --current --namespace=sk-rag-demo

cd app
helm upgrade --install frontendchart ./frontend/knowledgebasefrontendchart
helm upgrade --install documentprocessingchart ./backend/documentprocessingchart
helm upgrade --install knowledgeprocessingchart ./backend/knowledgeprocessingchart
helm upgrade --install searchknowledgechart ./backend/searchknowledgechart

kubectl rollout restart deployment frontend
kubectl rollout restart deployment document-processing
kubectl rollout restart deployment knowledge-processing
kubectl rollout restart deployment search-knowledge