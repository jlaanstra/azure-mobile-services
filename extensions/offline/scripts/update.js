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
    
    //updating happens here
    var processResult = function(result)
    {
        console.log(result);
        var response = {};

        response.__version = "";
                
        // put in right group
        if (result.isDeleted)
        {       
            response.results = [ ];     
            response.deleted = [result];
        }
        else
        {
            response.results = [result];
            response.deleted = [ ];
        }
                
        //we dont want to send deletion information
        delete result.isDeleted;
                
        request.respond(statusCodes.OK, response);     
    }

    var processResolution = function(resolvedItem)
    {
        item = resolvedItem;

        // you can't update a __version column
        delete item.__version;

        request.execute({
            systemProperties: ['*'],
            success: function () {
                processResult(item);
            },
            error: function (err) {
                console.error("Error occurred. Details:", err);
                request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
            }
        });
    }
    
    request.execute({
        systemProperties: ['*'],
        success: function()
        {
            processResult(item);
        },
        conflict: function (serverItem)
        {
            resolveConflict(serverItem, item, processResolution);
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