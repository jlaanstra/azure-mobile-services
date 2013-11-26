/// <reference path="mobileservices.intellisense.js" />

function update(item, user, request) {

    //existing items must have a timestamp
    if(item.__version === undefined)
    {
        request.respond(statusCodes.BAD_REQUEST,
        "update operation must have __version");
        return;
    }
    //item cannot set isDeleted
    if(item.isDeleted !== undefined)
    {
        request.respond(statusCodes.BAD_REQUEST,
        "item cannot set isDeleted, isDeleted is a reserved column name");
        return;
    }
    
    var tableName = tables.current.getTableName();
    
    var sql = "SELECT * FROM " + tableName + " WHERE id = ?";
    
    //updating happens here
    var processResult = function(result)
    {
        item = result;
        // you can't update a timestamp column
        delete item.timestamp;
        
        request.execute({
            systemProperties: ['__version'],
            success: function(newItem)
            {
                var response = {};
                
                // put in right group
                if (newItem.isDeleted)
                {       
                    response.results = [ ];     
                    response.deleted = [newItem];
                }
                else
                {
                    response.results = [newItem];
                    response.deleted = [ ];
                }
                
                //we dont want to send deletion information
                delete newItem.isDeleted;
                
                request.respond(statusCodes.OK, response);
            },
            error: function (err)
            {
                console.error("Error occurred. Details:", err);
                request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
            }
        });        
    }
    
    request.execute({
        systemProperties: ['__version'],
        success: function(results)
        {            
            var result = results[0]
            //make hex string of timestamp
            result.timestamp = result.timestamp.toString('hex');
            
            if(result.timestamp == item.timestamp)
            { 
                processResult(item);
            }
            else if(result.timestamp > item.timestamp)
            {
                resolveConflict(result, item, processResult);
            }
            // client item is not known by the server
            else
            {
                request.respond(statusCodes.BAD_REQUEST);
            }  
        },
        conflict: function (serverItem)
        {
            resolveConflict(serverItem, item, processResult);
        },
        error: function (err)
        {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });    

}

//handle conflicts
function resolveConflict(currentItem, newItem, resolvedCallback)
{
    if(currentItem.isDeleted)
    {
        resolvedCallback(currentItem);
    }
    //for now last write wins in other cases
    resolvedCallback(newItem);
}