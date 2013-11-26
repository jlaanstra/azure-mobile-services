/// <reference path="mobileservices.intellisense.js" />

function read(query, user, request) {

    var timestamp = request.parameters.timestamp;
    
    var requestHasTimestamp = false;
    if(timestamp !== undefined)
    {
        requestHasTimestamp = true;
    }
    
    request.execute({
        systemProperties: ['__version'],
        success: function (results)
        {
            var response = {};
            if(results.totalCount !== undefined)
            {            
                response.count = results.totalCount;
            }
            
            // get latest timestamp
            mssql.query("SELECT @@DBTS", {
                success: function(result)
                {          
                    response.timestamp = result[0].Column0.toString('hex');
                    
                    var deleted = [];
                    var nondeleted = [];
                    
                    results.map(function(r) {
                        var isDeleted = r.isDeleted;
                        delete r.isDeleted;
                        if(!requestHasTimestamp)
                        {
                            if(!isDeleted)
                            {
                                nondeleted.push(r);                                
                            }
                        }
                        else if(r.__version > timestamp)
                        {
                            if(!isDeleted)
                            {
                                nondeleted.push(r);                                
                            }
                            else
                            {
                                deleted.push(r.guid);
                            }
                        }
                    });
            
                    response.results = nondeleted;
                    response.deleted = deleted;
                    request.respond(200,response);
                },
                error: function(err)
                {
                    console.error("Error occurred. Details:", err);
                    request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
                }
            });
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}