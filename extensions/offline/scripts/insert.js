/// <reference path="mobileservices.intellisense.js" />

function insert(item, user, request) {

    //new items cannot have a timestamp
    if(item.__version)
    {
        request.respond(statusCodes.BAD_REQUEST,
        "insert operation cannot have __version");
        return;
    }
    //item cannot set isDeleted
    if(item.isDeleted)
    {
        request.respond(statusCodes.BAD_REQUEST,
        "item cannot set isDeleted, isDeleted is a reserved column name");
        return;
    }
        
    // a newly inserted item is not deleted
    item.isDeleted = false;
    
    request.execute({
        systemProperties: ['*'],
        success: function ()
        {
            var result = item;
            var response = {};
            
            // copy totalcount over
            if(result.totalCount !== undefined)
            {            
                response.count = result.totalCount;
            }            
            
            //we dont want to send deletion information
            delete result.isDeleted;
            response.results = [ result ];
            response.deleted = [];
            
            request.respond(statusCodes.OK, response);
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            //2627: key already exists
            if (err.code == 2627)
            {
                request.respond(statusCodes.CONFLICT, err);
            }
            else
            {
                request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
            }            
        }
    });
}