#!/bin/bash

cwd=$(dirname "${BASH_SOURCE[0]}")

for file in $(ls ${cwd}/params/*.json); do
    envName=$(echo $file | xargs basename | sed "s/\.json//")
    params=$(cat $file)
    params=$(echo $params | jq ".Image=\"$1\"")
    
    config={}
    config=$(echo $config | jq --argjson params "$params" '.Parameters=$params')
    echo $config > mutedac.${envName}.config.json
done