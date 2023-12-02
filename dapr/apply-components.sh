for file in ./components/*.yml
do
kubectl apply -f $file
done