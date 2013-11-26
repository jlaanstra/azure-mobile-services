/// <reference path="mobileservices.intellisense.js" />

function del(id, user, request) {
    
    var tableName = tables.current.getTableName();
    
    //only delete if false to safe an unnecessary changes
    var sqlUpdate = "UPDATE " + tableName + " SET isDeleted = 'True' WHERE id = ? AND isDeleted = 'False'";
        
    mssql.query(sqlUpdate, {
        success: function()
        {   
            var response = {};
                    
            response.results = [];                 
            response.deleted = [id];
            request.respond(statusCodes.OK, response); 
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });    
}