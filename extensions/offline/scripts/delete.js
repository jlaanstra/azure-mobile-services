/// <reference path="mobileservices.intellisense.js" />

function del(id, user, request) {

    var version = request.parameters.version;
    //existing items must have a timestamp
    if (version === undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "delete operation must specify the version parameter");
        return;
    }
    
    var tableName = tables.current.getTableName();
    
    //only delete if false to safe an unnecessary change
    var sqlUpdate = "UPDATE " + tableName + " SET isDeleted = 'True' WHERE id = ? AND isDeleted = 'False'";
        
    mssql.query(sqlUpdate, {
        success: function()
        {   
            var response = {};
                    
            response.__version = "";
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