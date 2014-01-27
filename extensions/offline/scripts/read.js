/// <reference path="mobileservices.intellisense.js" />

function read(query, user, request) {

    var version = request.parameters.version;

    var requestHasVersion = version !== undefined;

    request.execute({
        systemProperties: ['*'],
        success: function (results) {
            var response = {};
            if (results.totalCount !== undefined) {
                response.count = results.totalCount;
            }

            // get latest version
            mssql.query("SELECT @@DBTS", {
                success: function (result) {
                    response.__version = result[0].Column0.toString('base64');

                    var deleted = [];
                    var nondeleted = [];

                    results.map(function (r) {
                        var isDeleted = r.isDeleted;
                        delete r.isDeleted;
                        if (!requestHasVersion) {
                            if (!isDeleted) {
                                nondeleted.push(r);
                            }
                        }
                        else if (r.__version > version) {
                            if (!isDeleted) {
                                nondeleted.push(r);
                            }
                            else {
                                deleted.push(r.guid);
                            }
                        }
                    });

                    response.results = nondeleted;
                    response.deleted = deleted;
                    request.respond(200, response);
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