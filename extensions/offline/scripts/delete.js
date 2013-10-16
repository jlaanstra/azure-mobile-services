/// <reference path="mobileservices.intellisense.js" />

function del(id, user, request) {
    
    var tableName = tables.current.getTableName();
    
    //only delete if false to safe unnecessary changes
    var sqlUpdate = "UPDATE " + tableName + " SET isDeleted = 'True' WHERE id = ? AND isDeleted = 'False'";
    var sqlSelect = "SELECT guid FROM " + tableName + " WHERE id = ?";
        
    mssql.query(sqlUpdate, [id], {
        success: function(result)
        {   
            mssql.query(sqlSelect, [id], {
                success: function(result)
                {
                    var response = {};
                    
                    response.results = [];
                    if(result.length > 0)
                    {                        
                        response.deleted = [result[0].guid];
                    }
                    else
                    {
                        response.deleted = [];
                    }
                    request.respond(statusCodes.OK, response);
                },
                error: function (err) {
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