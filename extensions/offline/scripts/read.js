/// <reference path="mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');

function read(query, user, request) {

    var requestVersion = request.parameters.version;

    var requestHasVersion = requestVersion !== undefined;
    if (!requestHasVersion) {
        query = query.where({
            isDeleted: false
        });
    } else {
        // this shit is awesome:
        // http://blogs.msdn.com/b/carlosfigueira/archive/2012/09/21/playing-with-the-query-object-in-read-operations-on-azure-mobile-services.aspx
        query = query.where(function (rVersion) {
            return this.__version > rVersion;
        }, requestVersion);
    }
    // request as much items as possible
    query = query.take(1000);


    request.execute({
        systemProperties: ['*'],
        success: function (results) {

            // get latest version
            mssql.query("SELECT @@DBTS", {
                success: function (result) {
                    var responseVersion = result[0].Column0.toString('base64');
                    var deleted = [];
                    var nondeleted = [];

                    console.log("result: " + results.length);
                    console.log("requestHasVersion: " + requestHasVersion);

                    results.map(function (r) {
                        if (!r.isDeleted) {
                            nondeleted.push(r);
                        }
                        else {
                            deleted.push(r.id);
                        }
                    });

                    var params = {};
                    if (results.totalCount !== undefined) {
                        params.count = results.totalCount;
                    }

                    console.log("nondeleted: " + nondeleted.length);
                    console.log("deleted: " + deleted.length);

                    responseHelper.sendSuccessResponse(request, responseVersion, nondeleted, deleted, params);
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