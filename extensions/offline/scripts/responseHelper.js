exports.sendSuccessResponse = function(request, version, results, deleted, params)
{
	var response = {};

	// copy params object properties over
	if (params !== undefined) {
		for (var item in params) {
			response[item] = params[item];
		}
	}

	//we dont want to send deletion information
	var withoutIsDeleted = results.map(function (r) {
	    delete r.isDeleted;
	    return r;
	});

	response.__version = version;
	response.results = withoutIsDeleted;
	response.deleted = deleted;

	request.respond(statusCodes.OK, response);
}

exports.sendConflictResponse = function(request, type, version, currentItem, newItem, params)
{
	var response = {};

	// copy params over
	if (params !== undefined) {
		for (var item in params) {
			response[item] = params[item];
		}
	}

	//we dont want to send deletion information
	delete currentItem.isDeleted;
	delete newItem.isDeleted;
	
	response.conflictType = type;
	response.__version = version;
	response.currentItem = currentitem;
	response.newItem = newItem;

	request.respond(statusCodes.CONFLICT, response);
}